import Phaser from "phaser";
import StartScene from "./scenes/startScene";
import LobbyScene from "./scenes/lobbyScene";
import GameScene from "./scenes/gameScene";

export const config: Phaser.Types.Core.GameConfig = {
  type: Phaser.AUTO,
  width: 1280,
  height: 820,
  backgroundColor: "#1a1a1a",
  scene: [StartScene, LobbyScene, GameScene],
};
