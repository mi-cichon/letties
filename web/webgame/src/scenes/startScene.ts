import Phaser from "phaser";
import { COLORS, JwtStorageKey, PlayerNameStorageKey } from "../constants";
import { createNicknameModal } from "./nicknameModal";
import { loginToGame, validateToken, clearAuth } from "../managers/auth";
import { createConnection, getConnection } from "../managers/signalR";
import { LobbyState, Player, JoinResponse } from "../models/types";

export default class StartScene extends Phaser.Scene {
  private statusText: Phaser.GameObjects.Text | null = null;

  constructor() {
    super({ key: "StartScene" });
  }

  create() {
    this.add
      .rectangle(0, 0, this.scale.width, this.scale.height, COLORS.darkBg)
      .setOrigin(0);

    this.add
      .text(this.scale.width / 2, this.scale.height / 2 - 100, "SCRABBLE", {
        fontSize: "64px",
        fontFamily: "Arial",
        color: "#ffffff",
        fontStyle: "bold",
      })
      .setOrigin(0.5);

    this.statusText = this.add
      .text(this.scale.width / 2, this.scale.height / 2, "Ładowanie...", {
        fontSize: "20px",
        fontFamily: "Arial",
        color: "#d4c5b9",
      })
      .setOrigin(0.5);

    this.initializeAuth();
  }

  private async initializeAuth() {
    const cachedToken = localStorage.getItem(JwtStorageKey);
    const cachedNick = localStorage.getItem(PlayerNameStorageKey);

    if (cachedToken && cachedNick) {
      this.updateStatus("Weryfikacja sesji...");
      const isValid = await validateToken();

      if (isValid) {
        this.updateStatus("Łączenie z serwerem...");
        await this.joinGame(cachedNick);
        return;
      } else {
        this.updateStatus("Sesja wygasła");
        clearAuth();
      }
    }

    this.showNicknamePrompt();
  }

  private showNicknamePrompt() {
    this.updateStatus("Podaj swój nick");

    createNicknameModal(async (nick: string) => {
      this.updateStatus("Logowanie...");

      const { success } = await loginToGame(nick);

      if (success) {
        this.updateStatus("Łączenie z serwerem...");
        await this.joinGame(nick);
      } else {
        this.updateStatus("Błąd logowania. Odśwież stronę.");
      }
    });
  }

  private async joinGame(nick: string) {
    try {
      const connection = await createConnection();

      const joinResponse = await connection.invoke<JoinResponse>("Join");

      // LobbyScene will use joinResponse.playerId and joinResponse.lobbyState
      // nick is already stored in localStorage via loginToGame
      this.scene.start("LobbyScene", {
        playerId: joinResponse.playerId,
        lobbyStateDetails: joinResponse.lobbyState,
        playerNick: nick,
      });
    } catch (error) {
      console.error("Join failed:", error);
      this.updateStatus("Nie można dołączyć do gry. Spróbuj ponownie.");

      setTimeout(() => {
        clearAuth();
        this.scene.restart();
      }, 2000);
    }
  }

  private updateStatus(message: string) {
    this.statusText?.setText(message);
  }
}
