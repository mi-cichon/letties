import Phaser from "phaser";
import {
  BOARD_SIZE,
  TILE_SIZE,
  BOARD_OFFSET_Y,
  COLORS,
  CHAT_WIDTH,
  CHAT_HEIGHT,
  CHAT_GAP,
} from "../constants";
import { createBoard, BoardCell } from "./board";
import { createChatUI, ChatCapableScene, ChatLayout } from "./chat";
import {
  getConnection,
  setupGameHandlers,
  removeGameHandlers,
} from "../managers/signalR";
import {
  Player,
  ChatMessage,
  GameState,
  GameTile,
  TilePlacement,
} from "../models/types";
import { HubConnectionState } from "@microsoft/signalr";

export default class GameScene
  extends Phaser.Scene
  implements ChatCapableScene
{
  board: BoardCell[][] = [];
  private currentPlayer: Player | null = null;
  private chatMessages: ChatMessage[] = [];
  private boardOffsetX = 80;
  private boardOffsetY = BOARD_OFFSET_Y;
  private gameState: GameState | null = null;
  private playerTiles: GameTile[] = [];
  private placedTiles: TilePlacement[] = [];

  chatInput?: HTMLInputElement;
  sendChatMessage = (message: string) => {
    const connection = getConnection();
    if (connection?.state === HubConnectionState.Connected) {
      connection.invoke("SendMessage", message).catch(console.error);
    }
  };

  constructor() {
    super({ key: "GameScene" });
  }

  init(data: { gameState: GameState }) {
    this.gameState = data.gameState;
  }

  create() {
    this.add
      .rectangle(0, 0, this.scale.width, this.scale.height, COLORS.darkBg)
      .setOrigin(0);

    this.createGameUI();
    this.setupBackendHandlers();
  }

  shutdown() {
    removeGameHandlers([
      "TilesReceived",
      "TilesPlaced",
      "TurnChanged",
      "WordSubmitted",
      "GameEnded",
      "ReceiveMessage",
    ]);
  }

  private setupBackendHandlers() {
    setupGameHandlers({
      TilesReceived: (tiles: GameTile[]) => {
        this.playerTiles = tiles;
        this.renderPlayerTiles();
      },

      TilesPlaced: (playerId: string, placements: TilePlacement[]) => {
        this.renderTilesOnBoard(placements);
        this.addChatMessage(
          "System",
          `Gracz ${playerId} ułożył ${placements.length} literek`,
        );
      },

      TurnChanged: (playerId: string) => {
        this.addChatMessage("System", `Teraz kolej gracza ${playerId}`);
      },

      WordSubmitted: (playerId: string, score: number) => {
        this.addChatMessage(
          "System",
          `Gracz ${playerId} zdobył ${score} punktów`,
        );
      },

      GameEnded: (winnerId: string, finalScores: Record<string, number>) => {
        this.addChatMessage("System", `Koniec gry! Wygrał ${winnerId}`);
        Object.entries(finalScores).forEach(([id, score]) => {
          this.addChatMessage("System", `${id}: ${score} punktów`);
        });
      },

      ReceiveMessage: (user: string, message: string) => {
        this.addChatMessage(user, message);
      },
    });
  }

  private renderPlayerTiles() {
    console.log("Received tiles:", this.playerTiles);
  }

  private renderTilesOnBoard(placements: TilePlacement[]) {
    console.log("Tiles placed on board:", placements);
  }

  private createGameUI() {
    const boardWidth = BOARD_SIZE * TILE_SIZE;

    this.add
      .text(this.boardOffsetX + boardWidth / 2, 30, "SCRABBLE", {
        fontSize: "32px",
        fontFamily: "Arial",
        color: "#ffffff",
        fontStyle: "bold",
      })
      .setOrigin(0.5, 0);

    createBoard(this, {
      x: this.boardOffsetX,
      y: this.boardOffsetY,
    });

    const chatX = this.boardOffsetX + boardWidth + CHAT_GAP;
    const chatLayout: ChatLayout = {
      x: chatX,
      y: this.boardOffsetY,
      width: CHAT_WIDTH,
      height: CHAT_HEIGHT,
    };

    createChatUI(this, chatLayout);
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
}
