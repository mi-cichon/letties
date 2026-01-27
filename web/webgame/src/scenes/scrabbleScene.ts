import Phaser from "phaser";
import * as signalR from "@microsoft/signalr";
import {
  BOARD_SIZE,
  TILE_SIZE,
  BOARD_OFFSET_X,
  BOARD_OFFSET_Y,
  RACK_Y,
  LETTERS,
  LETTER_VALUES,
  HUB_URLS,
  COLORS,
  JwtStorageKey,
  PlayerNameStorageKey,
  CHAT_WIDTH,
  CHAT_HEIGHT,
  CHAT_GAP,
} from "../constants";
import { createBoard, BoardCell, BoardTileData } from "./board";
import { createChatUI, ChatCapableScene, ChatLayout } from "./chat";
import { createNicknameModal } from "./nicknameModal";

interface ChatMessage {
  user: string;
  message: string;
  timestamp: number;
}

type Hub = signalR.HubConnection;

export default class ScrabbleScene
  extends Phaser.Scene
  implements ChatCapableScene
{
  board: BoardCell[][] = [];
  playerTiles: Array<BoardTileData | null> = [];
  score = 0;
  connection: Hub | null = null;
  playerId: string | null = null;
  gameId: string | null = null;
  playerName: string | null = null;
  waitingText: Phaser.GameObjects.Text | null = null;
  chatMessages: ChatMessage[] = [];
  chatDisplay?: Phaser.GameObjects.Text;
  chatInput?: HTMLInputElement;
  scoreText?: Phaser.GameObjects.Text;

  boardOffsetX = BOARD_OFFSET_X;
  boardOffsetY = BOARD_OFFSET_Y;
  rackY = RACK_Y;

  constructor() {
    super({ key: "ScrabbleScene" });
  }

  preload() {
    // Grafika będzie generowana programowo
  }

  create() {
    // Ekran startowy - sepiowe tło
    this.add
      .rectangle(0, 0, this.scale.width, this.scale.height, COLORS.darkBg)
      .setOrigin(0);

    // Tekst statusu
    this.waitingText = this.add
      .text(450, 350, "", {
        fontSize: "22px",
        fontFamily: "Arial",
        color: "#ffffff",
      })
      .setOrigin(0.5);

    const cachedToken = localStorage.getItem(JwtStorageKey);
    const cachedName = localStorage.getItem(PlayerNameStorageKey);

    if (cachedToken && cachedName) {
      this.playerName = cachedName;
      this.initializeGame(cachedName);
    } else {
      createNicknameModal((nick) => {
        this.playerName = nick;
        this.waitingText?.setText(`Logowanie jako ${this.playerName}...`);
        this.acquireTokenFromServer(nick).then(() => {
          this.initializeGame(nick);
          localStorage.setItem(PlayerNameStorageKey, nick);
        });
      });
    }
  }

  initializeGame(user: string) {
    this.addChatMessage("System", `Witaj, ${user}! Łączenie z serwerem...`);

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URLS.game, {
        accessTokenFactory: () => localStorage.getItem(JwtStorageKey) || "",
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.connection
      .start()
      .then(() => this.connection?.invoke("Join"))
      .then((playerId?: string) => {
        if (playerId) this.playerId = playerId;
        this.waitingText?.setText("Połączono. Ładowanie gry...");
        this.initGameUI();
        this.addChatMessage("System", "Połączono");
        this.setupGameHubHandlers();
      })
      .catch((err) => {
        console.error("Błąd inicjalizacji:", err);
        this.waitingText?.setText("Błąd. Odśwież i spróbuj ponownie.");
      });
  }

  acquireTokenFromServer(name: string) {
    const loginConnection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URLS.login)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    return loginConnection
      .start()
      .then(() => loginConnection.invoke("login", name))
      .then((token: string) => {
        localStorage.setItem(JwtStorageKey, token);
        loginConnection.stop();
      })
      .catch((err) => {
        console.error("Błąd pozyskiwania tokena:", err);
      });
  }

  setupGameHubHandlers() {
    if (!this.connection) return;

    this.connection.on("ReceiveMessage", (user: string, message: string) => {
      this.addChatMessage(user, message);
    });

    this.connection.onreconnecting((error: Error | undefined) => {
      console.log("Reconnecting...", error);
      this.addChatMessage("System", "Łączenie ponownie...");
    });

    this.connection.onreconnected((connectionId: string | undefined) => {
      console.log("Reconnected:", connectionId);
      this.addChatMessage("System", "Połączono ponownie");
    });

    this.connection.onclose((error: Error | undefined) => {
      console.log("Połączenie zamknięte:", error);
      this.addChatMessage("System", "Rozłączono");
    });
  }

  initGameUI() {
    // Wyczyszczenie ekranu startowego
    this.children.removeAll();

    // Tło sepiowe
    this.add
      .rectangle(0, 0, this.scale.width, this.scale.height, COLORS.darkBg)
      .setOrigin(0);

    const boardWidth = BOARD_SIZE * TILE_SIZE;
    const totalWidth = boardWidth + CHAT_GAP + CHAT_WIDTH;

    // Center the board as much as possible, then nudge left only if chat would overflow.
    const idealBoardOffset = (this.scale.width - boardWidth) / 2;
    const maxBoardOffsetToFitChat = this.scale.width - totalWidth;

    this.boardOffsetX = Math.max(
      20,
      Math.min(idealBoardOffset, maxBoardOffsetToFitChat),
    );
    this.boardOffsetY = BOARD_OFFSET_Y;
    this.rackY = this.boardOffsetY + boardWidth + 30;

    const boardCenterX = this.boardOffsetX + boardWidth / 2;
    const headerY = 30;
    const chatX = this.boardOffsetX + boardWidth + CHAT_GAP;
    const chatCenterX = chatX + CHAT_WIDTH / 2;

    // Tytuł
    this.add
      .text(boardCenterX, headerY, "SCRABBLE", {
        fontSize: "32px",
        fontFamily: "Arial",
        color: "#ffffff",
        fontStyle: "bold",
      })
      .setOrigin(0.5, 0);

    // Wyświetlanie punktów (przy czacie)
    this.scoreText = this.add
      .text(chatCenterX, headerY, "Punkty: 0", {
        fontSize: "20px",
        fontFamily: "Arial",
        color: "#ffffff",
      })
      .setOrigin(0.5, 0);

    createBoard(this, { x: this.boardOffsetX, y: this.boardOffsetY });
    this.createPlayerRack();
    this.drawInitialTiles();

    // Przyciski na prawo od literek, na tym samym poziomie
    const rackEndX = this.boardOffsetX + TILE_SIZE * 7;
    const button1X = rackEndX + 120;
    const button2X = button1X + 160;
    const buttonY = this.rackY + 20;

    // Helper function to create a styled button
    const createStyledButton = (
      x: number,
      y: number,
      width: number,
      height: number,
      color: number,
      label: string,
      callback: () => void,
    ) => {
      // Shadow
      this.add
        .rectangle(x + 3, y + 3, width, height, 0x000000)
        .setAlpha(0.3)
        .setDepth(0);

      // Main button with stroke
      const button = this.add
        .rectangle(x, y, width, height, color)
        .setStrokeStyle(2, 0xffffff)
        .setInteractive({ useHandCursor: true })
        .setDepth(1);

      // Button text
      const text = this.add
        .text(x, y, label, {
          fontSize: "16px",
          fontFamily: "Arial",
          color: "#ffffff",
          fontStyle: "bold",
        })
        .setOrigin(0.5)
        .setDepth(2);

      // Hover effects
      button.on("pointerover", () => {
        button.setScale(1.05);
        text.setScale(1.05);
      });

      button.on("pointerout", () => {
        button.setScale(1);
        text.setScale(1);
      });

      button.on("pointerdown", () => {
        button.setScale(0.98);
      });

      button.on("pointerup", () => {
        button.setScale(1.05);
        callback();
      });
    };

    // Przycisk "Zaakceptuj" - zielony (pozytywny)
    createStyledButton(
      button1X,
      buttonY,
      140,
      44,
      COLORS.buttonSuccess,
      "Zaakceptuj",
      () => this.submitWord(),
    );

    // Przycisk "Nowe litery" - pomarańczowy (ostrzezenie)
    createStyledButton(
      button2X,
      buttonY,
      140,
      44,
      COLORS.buttonWarning,
      "Nowe litery",
      () => this.drawNewTiles(),
    );

    // Czat panel
    const chatLayout: ChatLayout = {
      x: chatX,
      y: this.boardOffsetY,
      width: CHAT_WIDTH,
      height: CHAT_HEIGHT,
    };
    createChatUI(this, chatLayout);
  }

  updateConnectionStatus(message: string) {
    // Kept for compatibility
    this.addChatMessage("System", message);
  }

  createPlayerRack() {
    this.add
      .rectangle(
        this.boardOffsetX,
        this.rackY,
        TILE_SIZE * 7,
        TILE_SIZE + 10,
        COLORS.rackBase,
      )
      .setStrokeStyle(3, COLORS.rackStroke)
      .setOrigin(0);
  }

  drawInitialTiles() {
    for (let i = 0; i < 7; i++) {
      this.createTile(i);
    }
  }

  drawNewTiles() {
    this.playerTiles.forEach((tile) => {
      if (tile) {
        tile.sprite.destroy();
        tile.text.destroy();
        tile.valueText.destroy();
      }
    });
    this.playerTiles = [];
    this.drawInitialTiles();
  }

  createTile(rackPosition: number) {
    const letter = this.getRandomLetter();
    const x = this.boardOffsetX + rackPosition * TILE_SIZE + TILE_SIZE / 2;
    const y = this.rackY + TILE_SIZE / 2;

    const tile = this.add
      .rectangle(x, y, TILE_SIZE - 4, TILE_SIZE - 4, COLORS.tileFill)
      .setInteractive({ draggable: true })
      .setStrokeStyle(2, COLORS.tileStroke);

    const text = this.add
      .text(x, y - 3, letter, {
        fontSize: "20px",
        fontFamily: "Arial",
        color: "#000000",
        fontStyle: "bold",
      })
      .setOrigin(0.5);

    const valueText = this.add
      .text(x + 10, y + 8, LETTER_VALUES[letter].toString(), {
        fontSize: "10px",
        fontFamily: "Arial",
        color: "#000000",
      })
      .setOrigin(0.5);

    const tileData: BoardTileData = {
      sprite: tile,
      text,
      valueText,
      letter,
      rackPosition,
      originalX: x,
      originalY: y,
      placedOnBoard: false,
      boardX: null,
      boardY: null,
    };

    this.playerTiles[rackPosition] = tileData;

    tile.on(
      "drag",
      (_pointer: Phaser.Input.Pointer, dragX: number, dragY: number) => {
        tile.setPosition(dragX, dragY);
        text.setPosition(dragX, dragY - 3);
        valueText.setPosition(dragX + 10, dragY + 8);
      },
    );

    tile.on("dragend", (pointer: Phaser.Input.Pointer) => {
      const gridPos = this.getGridPosition(pointer.x, pointer.y);

      if (gridPos && !this.board[gridPos.row][gridPos.col].letter) {
        const snapX =
          this.boardOffsetX + gridPos.col * TILE_SIZE + TILE_SIZE / 2;
        const snapY =
          this.boardOffsetY + gridPos.row * TILE_SIZE + TILE_SIZE / 2;

        tile.setPosition(snapX, snapY);
        text.setPosition(snapX, snapY - 3);
        valueText.setPosition(snapX + 10, snapY + 8);

        tileData.placedOnBoard = true;
        tileData.boardX = gridPos.col;
        tileData.boardY = gridPos.row;
        this.board[gridPos.row][gridPos.col].letter = tileData;
      } else {
        tile.setPosition(tileData.originalX, tileData.originalY);
        text.setPosition(tileData.originalX, tileData.originalY - 3);
        valueText.setPosition(tileData.originalX + 10, tileData.originalY + 8);

        if (
          tileData.placedOnBoard &&
          tileData.boardY !== null &&
          tileData.boardX !== null
        ) {
          this.board[tileData.boardY][tileData.boardX].letter = null;
          tileData.placedOnBoard = false;
        }
      }
    });

    this.input.setDraggable(tile);
  }

  getGridPosition(x: number, y: number) {
    const col = Math.floor((x - this.boardOffsetX) / TILE_SIZE);
    const row = Math.floor((y - this.boardOffsetY) / TILE_SIZE);

    if (row >= 0 && row < BOARD_SIZE && col >= 0 && col < BOARD_SIZE) {
      return { row, col };
    }
    return null;
  }

  getRandomLetter() {
    return LETTERS[Math.floor(Math.random() * LETTERS.length)];
  }

  submitWord() {
    let points = 0;
    const tilesPlaced: BoardTileData[] = [];

    this.playerTiles.forEach((tile) => {
      if (tile && tile.placedOnBoard) {
        points += LETTER_VALUES[tile.letter];
        tilesPlaced.push(tile);
      }
    });

    if (tilesPlaced.length > 0) {
      this.score += points;
      this.scoreText?.setText("Punkty: " + this.score);

      tilesPlaced.forEach((tile) => {
        const idx = this.playerTiles.indexOf(tile);
        if (idx !== -1) {
          this.playerTiles[idx] = null;
        }
      });

      this.playerTiles.forEach((tile, idx) => {
        if (!tile) {
          this.createTile(idx);
        }
      });
    }
  }

  addChatMessage(user: string, message: string) {
    const maxMessages = 12;
    this.chatMessages.push({ user, message, timestamp: Date.now() });

    if (this.chatMessages.length > maxMessages) {
      this.chatMessages.shift();
    }

    this.updateChatDisplay();
  }

  updateChatDisplay() {
    const displayText = this.chatMessages
      .map((msg) => `${msg.user}: ${msg.message}`)
      .join("\n");

    this.chatDisplay?.setText(displayText);
  }

  sendChatMessage = (message: string) => {
    if (
      !this.connection ||
      this.connection.state !== signalR.HubConnectionState.Connected
    ) {
      console.error("Brak połączenia z serwerem");
      return;
    }

    this.connection
      .invoke("SendMessage", this.playerId, message)
      .catch((err) => {
        console.error("Błąd wysyłania wiadomości:", err);
      });
  };
}
