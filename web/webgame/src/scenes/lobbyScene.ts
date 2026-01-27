import Phaser from "phaser";
import { COLORS, CHAT_WIDTH, CHAT_HEIGHT, CHAT_GAP } from "../constants";
import { createChatUI, ChatCapableScene, ChatLayout } from "./chat";
import {
  Player,
  Seat,
  ChatMessage,
  LobbyState,
  LobbySeatDetails,
  LobbyPlayerDetails,
} from "../models/types";
import {
  getConnection,
  setupGameHandlers,
  removeGameHandlers,
} from "../managers/signalR";
import { HubConnectionState } from "@microsoft/signalr";

export default class LobbyScene
  extends Phaser.Scene
  implements ChatCapableScene
{
  private players: Player[] = [];
  private seats: Seat[] = [];
  private playerId: string = "";
  private playerNick: string = "";
  private chatMessages: ChatMessage[] = [];
  private seatLabels: Map<string, Phaser.GameObjects.Text> = new Map();

  // Layout cache for aligning elements
  private seatsStartX = 0;
  private seatsStartY = 0;
  private seatSizePx = 112;
  private seatGapPx = 24;
  private seatsGridWidth = 0;

  chatInput?: HTMLInputElement;
  sendChatMessage = (message: string) => {
    const connection = getConnection();
    if (connection?.state === HubConnectionState.Connected) {
      connection.invoke("SendMessage", message).catch(console.error);
    }
  };

  constructor() {
    super({ key: "LobbyScene" });
  }

  init(data: {
    playerId: string;
    lobbyStateDetails: any; // Will be LobbyStateDetails from backend
    playerNick: string;
  }) {
    this.playerId = data.playerId;
    this.playerNick = data.playerNick;

    if (data.lobbyStateDetails) {
      // Convert LobbySeatDetails to Seat format
      this.seats = (data.lobbyStateDetails.seats || []).map(
        (seatDetail: LobbySeatDetails) => ({
          seatId: seatDetail.seatId,
          playerId: seatDetail.playerId,
          isAdmin: seatDetail.isAdmin,
          order: seatDetail.order,
          player: null, // Will be populated from players list
        }),
      );

      // Convert LobbyPlayerDetails to Player format
      this.players = (data.lobbyStateDetails.players || []).map(
        (playerDetail: LobbyPlayerDetails) => ({
          id: playerDetail.playerId,
          playerName: playerDetail.playerName,
          role: "PLAYER" as const,
        }),
      );

      // Link players to seats
      this.seats.forEach((seat) => {
        if (seat.playerId) {
          const player = this.players.find((p) => p.id === seat.playerId);
          if (player) {
            seat.player = player;
            // Update role based on isAdmin
            if (seat.isAdmin) {
              player.role = "ADMIN" as const;
            }
          }
        }
      });
    }
  }

  create() {
    this.add
      .rectangle(0, 0, this.scale.width, this.scale.height, COLORS.darkBg)
      .setOrigin(0);

    this.initializeSeats();
    this.createLobbyUI();
    this.createChatUI();
    this.createPlayerList();
    removeGameHandlers([
      "PlayerJoined",
      "PlayerLeft",
      "SeatTaken",
      "SeatLeft",
      "ReceiveMessage",
      "GameStarted",
      "PlayerEnteredSeat",
      "PlayerLeftSeat",
    ]);
    this.setupBackendHandlers();
  }

  shutdown() {
    removeGameHandlers([
      "PlayerJoined",
      "PlayerLeft",
      "SeatTaken",
      "SeatLeft",
      "ReceiveMessage",
      "GameStarted",
    ]);
  }

  private setupBackendHandlers() {
    setupGameHandlers({
      PlayerJoined: (player: Player) => {
        this.players.push(player);
        this.updatePlayerList();
        this.addChatMessage("System", `${player.playerName} dołączył do stołu`);
      },

      PlayerLeft: (playerId: string) => {
        this.players = this.players.filter((p) => p.id !== playerId);
        this.updatePlayerList();
      },

      SeatTaken: (seatId: string, player: Player) => {
        const seat = this.seats.find((s) => s.seatId === seatId);
        if (seat) {
          seat.playerId = player.id;
          seat.player = player;
          if (seat.isAdmin) {
            player.role = "ADMIN" as const;
          }
          this.refreshSeat(seatId);
        }
      },

      SeatLeft: (seatId: string) => {
        const seat = this.seats.find((s) => s.seatId === seatId);
        if (seat) {
          seat.playerId = null;
          seat.player = null;
          this.refreshSeat(seatId);
        }
      },

      ReceiveMessage: (user: string, message: string) => {
        this.addChatMessage(user, message);
      },

      GameStarted: (gameState) => {
        this.scene.start("GameScene", { gameState });
      },

      PlayerEnteredSeat: (seat: LobbySeatDetails) => {
        // Update seat info
        const s = this.seats.find((x) => x.seatId === seat.seatId);
        if (s) {
          s.playerId = seat.playerId;
          s.player = seat.playerId
            ? this.players.find((p) => p.id === seat.playerId) || null
            : null;
        }
        // Usuwamy duplikaty graczy na liście (mogą się pojawić po reconnect)
        this.players = this.players.filter(
          (p, idx, arr) => arr.findIndex((pp) => pp.id === p.id) === idx,
        );
        this.refreshSeat(seat.seatId);
        this.updatePlayerList();
      },

      PlayerLeftSeat: (seat: LobbySeatDetails) => {
        const s = this.seats.find((x) => x.seatId === seat.seatId);
        if (s) {
          s.playerId = null;
          s.player = null;
        }
        this.refreshSeat(seat.seatId);
        this.updatePlayerList();
      },
    });
  }

  private initializeSeats() {
    // No-op: seats are now initialized from backend response
  }

  private createLobbyUI() {
    // Layout: one row of 4 seats, centered before the chat panel
    const seatsPerRow = 4;
    const seatSize = this.seatSizePx;
    const gap = this.seatGapPx;
    const startY = 130;

    // Determine available width before chat panel
    const chatX = this.scale.width - CHAT_WIDTH - 40;
    const leftMargin = 60;
    const leftAreaRight = chatX - CHAT_GAP;
    const leftWidth = leftAreaRight - leftMargin;

    const gridWidth = seatsPerRow * seatSize + (seatsPerRow - 1) * gap;
    const startX = leftMargin + Math.max(0, (leftWidth - gridWidth) / 2);

    // Cache for other sections (player list)
    this.seatsStartX = startX;
    this.seatsStartY = startY;
    this.seatSizePx = seatSize;
    this.seatGapPx = gap;
    this.seatsGridWidth = gridWidth;

    this.add
      .text(this.scale.width / 2, 40, "LOBBY", {
        fontSize: "40px",
        fontFamily: "Arial",
        color: "#ffffff",
        fontStyle: "bold",
      })
      .setOrigin(0.5, 0);

    // Sort seats by order from backend
    const sortedSeats = [...this.seats].sort((a, b) => a.order - b.order);

    sortedSeats.forEach((seat, index) => {
      const col = index % seatsPerRow;
      const row = Math.floor(index / seatsPerRow);
      const x = startX + col * (seatSize + gap);
      const y = startY + row * (seatSize + gap);

      this.createSeatUI(x, y, seatSize, seat);
    });
  }

  private createSeatUI(x: number, y: number, size: number, seat: Seat) {
    const isOccupied = seat.player !== null;
    const bgColor = isOccupied ? COLORS.accentBrown : COLORS.rackBase;

    const seatBg = this.add
      .rectangle(x + size / 2, y + size / 2, size, size, bgColor)
      .setStrokeStyle(2, 0xffffff);

    const seatContainer = this.add.container(x + size / 2, y + size / 2);

    if (isOccupied && seat.player) {
      const nameText = this.add
        .text(0, -20, seat.player.playerName, {
          fontSize: "16px",
          fontFamily: "Arial",
          color: "#ffffff",
          fontStyle: "bold",
        })
        .setOrigin(0.5)
        .setDepth(2);
      seatContainer.add(nameText);

      const roleText = this.add
        .text(0, 10, seat.player.role, {
          fontSize: "12px",
          fontFamily: "Arial",
          color: "#d4c5b9",
        })
        .setOrigin(0.5)
        .setDepth(2);
      seatContainer.add(roleText);
    } else {
      const hintText = this.add
        .text(0, 0, "Kliknij aby\ndołączyć", {
          fontSize: "14px",
          fontFamily: "Arial",
          color: "#a0a0a0",
          align: "center",
        })
        .setOrigin(0.5)
        .setDepth(2);
      seatContainer.add(hintText);
    }

    if (!isOccupied) {
      seatBg
        .setInteractive({ useHandCursor: true })
        .on("pointerover", () => {
          seatBg.setStrokeStyle(3, 0xffffff);
        })
        .on("pointerout", () => {
          seatBg.setStrokeStyle(2, 0xffffff);
        })
        .on("pointerdown", () => {
          this.onSeatClick(seat);
        });
    }
    // Label pod siedzeniem z informacją kto siedzi / czy puste
    const roleHint = seat.isAdmin ? "ADMIN" : "PLAYER";
    const labelText =
      isOccupied && seat.player
        ? `${seat.player.playerName} (${seat.player.role})`
        : `Puste • ${roleHint}`;

    const label = this.add
      .text(x + size / 2, y + size + 8, labelText, {
        fontSize: "12px",
        fontFamily: "Arial",
        color: "#d4c5b9",
      })
      .setOrigin(0.5, 0);

    this.seatLabels.set(seat.seatId, label);
  }

  private onSeatClick(seat: Seat) {
    if (seat.player) return;

    const connection = getConnection();
    if (connection?.state === HubConnectionState.Connected) {
      connection
        .invoke("EnterSeat", seat.seatId)
        .then((success: boolean) => {
          if (!success) {
            this.addChatMessage("System", "Nie udało się zająć miejsca");
          }
        })
        .catch(console.error);
    }
  }

  private refreshSeat(seatId: string) {
    // Odśwież tylko UI danego siedzenia
    // Znajdź seat w sortedSeats, przelicz jego pozycję i wywołaj createSeatUI tylko dla niego
    const seatsPerRow = 4;
    const seatSize = this.seatSizePx;
    const gap = this.seatGapPx;
    const startY = this.seatsStartY;
    const startX = this.seatsStartX;
    const sortedSeats = [...this.seats].sort((a, b) => a.order - b.order);
    const index = sortedSeats.findIndex((s) => s.seatId === seatId);
    if (index === -1) return;
    const col = index % seatsPerRow;
    const row = Math.floor(index / seatsPerRow);
    const x = startX + col * (seatSize + gap);
    const y = startY + row * (seatSize + gap);
    // Usuwamy stary label jeśli był
    const oldLabel = this.seatLabels.get(seatId);
    if (oldLabel) oldLabel.destroy();
    this.createSeatUI(x, y, seatSize, sortedSeats[index]);
  }

  private updatePlayerList() {
    // Usuwa tylko stare teksty z listy graczy
    this.children.list
      .filter(
        (obj) =>
          obj.type === "Text" &&
          ((obj as Phaser.GameObjects.Text).text.startsWith(
            "Gracze na stole",
          ) ||
            (obj as Phaser.GameObjects.Text).style?.fontSize === "12px"),
      )
      .forEach((obj) => obj.destroy());
    this.createPlayerList();
  }

  private addChatMessage(user: string, message: string) {
    this.chatMessages.push({ user, message, timestamp: Date.now() });
    this.updateChatDisplay();
  }

  private updateChatDisplay() {
    const displayText = this.chatMessages
      .map((msg) => `${msg.user}: ${msg.message}`)
      .join("\n");

    const chatDiv = (this as any).chatDisplayDiv as HTMLDivElement;
    if (chatDiv) {
      chatDiv.textContent = displayText;
      // Auto-scroll to bottom after DOM updates
      setTimeout(() => {
        chatDiv.scrollTop = chatDiv.scrollHeight;
      }, 0);
    }
  }

  private createPlayerList() {
    // Align list directly under the seats row
    const listWidth = this.seatsGridWidth || 480;
    const listX = this.seatsStartX || 60;
    const topY = this.seatsStartY + this.seatSizePx + 70;
    const listHeight = 160;

    this.add
      .rectangle(
        listX + listWidth / 2,
        topY + listHeight / 2,
        listWidth,
        listHeight,
        COLORS.rackBase,
      )
      .setStrokeStyle(2, 0xffffff);

    this.add
      .text(listX + listWidth / 2, topY + 8, "Gracze na stole", {
        fontSize: "14px",
        fontFamily: "Arial",
        color: "#ffffff",
        fontStyle: "bold",
      })
      .setOrigin(0.5, 0);

    // Find playerId of admin (seat with isAdmin)
    const adminSeat = this.seats.find((s) => s.isAdmin && s.playerId);
    const adminId = adminSeat?.playerId;

    const playerText = this.players
      .map((p) => `${p.playerName}${p.id === adminId ? " (Admin)" : ""}`)
      .join("\n");

    this.add
      .text(listX + 10, topY + 32, playerText || "Brak graczy", {
        fontSize: "12px",
        fontFamily: "Arial",
        color: "#d4c5b9",
        wordWrap: { width: listWidth - 20 },
        lineSpacing: 4,
      })
      .setOrigin(0, 0);
  }

  private createChatUI() {
    const boardWidth = 320;
    const totalWidth = boardWidth + CHAT_GAP + CHAT_WIDTH;
    const chatX = this.scale.width - CHAT_WIDTH - 40;
    const chatY = 100;

    const chatLayout: ChatLayout = {
      x: chatX,
      y: chatY,
      width: CHAT_WIDTH,
      height: CHAT_HEIGHT,
    };

    createChatUI(this, chatLayout);
  }
}
