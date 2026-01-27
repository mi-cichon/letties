import {
  BOARD_SIZE,
  TILE_SIZE,
  BOARD_OFFSET_X,
  BOARD_OFFSET_Y,
  COLORS,
} from "../constants";
import Phaser from "phaser";

export interface BoardCell {
  cell: Phaser.GameObjects.Rectangle;
  letter: TileData | null;
  gridX: number;
  gridY: number;
}

export interface TileData {
  sprite: Phaser.GameObjects.Rectangle;
  text: Phaser.GameObjects.Text;
  valueText: Phaser.GameObjects.Text;
  letter: string;
  rackPosition: number;
  originalX: number;
  originalY: number;
  placedOnBoard: boolean;
  boardX: number | null;
  boardY: number | null;
}

export interface BoardScene extends Phaser.Scene {
  board: BoardCell[][];
}

export function createBoard(
  scene: BoardScene,
  offsets: { x: number; y: number } = {
    x: BOARD_OFFSET_X,
    y: BOARD_OFFSET_Y,
  },
) {
  scene.board = [];

  const offsetX = offsets.x;
  const offsetY = offsets.y;

  for (let row = 0; row < BOARD_SIZE; row++) {
    scene.board[row] = [];
    for (let col = 0; col < BOARD_SIZE; col++) {
      const x = offsetX + col * TILE_SIZE;
      const y = offsetY + row * TILE_SIZE;

      let color: number = COLORS.boardLight;
      if (row === 7 && col === 7) color = COLORS.centerSquare;

      const cell = scene.add
        .rectangle(x, y, TILE_SIZE - 2, TILE_SIZE - 2, color)
        .setOrigin(0)
        .setStrokeStyle(1, COLORS.boardStroke);

      scene.board[row][col] = {
        cell,
        letter: null,
        gridX: col,
        gridY: row,
      } as BoardCell;
    }
  }
}

// Re-export type for reuse in scrabbleScene
export type { TileData as BoardTileData };
