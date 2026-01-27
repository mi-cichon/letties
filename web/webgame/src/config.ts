import Phaser from "phaser";
import ScrabbleScene from "./scenes/scrabbleScene";

export const config: Phaser.Types.Core.GameConfig = {
  type: Phaser.AUTO,
  width: 1280,
  height: 820,
  backgroundColor: "#1a1a1a",
  scene: ScrabbleScene,
};
