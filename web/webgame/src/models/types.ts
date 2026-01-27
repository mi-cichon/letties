export interface Player {
  id: string;
  playerName: string;
  role: PlayerRole;
}

export type PlayerRole = "ADMIN" | "PLAYER" | "SPECTATOR";

export interface Seat {
  seatId: string;
  playerId: string | null;
  isAdmin: boolean;
  order: number;
  player?: Player | null; // For display convenience
}

export interface GameTile {
  id: string;
  letter: string;
  value: number;
}

export interface GameState {
  gameId: string;
  players: Player[];
  tiles: GameTile[];
  board: string[][];
}

export interface ChatMessage {
  user: string;
  message: string;
  timestamp: number;
}

export interface JoinResponse {
  playerId: string;
  lobbyState: LobbyStateDetails;
}

export interface LobbyStateDetails {
  players: LobbyPlayerDetails[];
  seats: LobbySeatDetails[];
  lobbyId: string;
}

export interface LobbyPlayerDetails {
  playerId: string;
  playerName: string;
}

export interface LobbySeatDetails {
  seatId: string;
  playerId: string | null;
  isAdmin: boolean;
  order: number;
}

export interface LobbyState {
  tableId: string;
  seats: Seat[];
  spectators: Player[];
}

export interface TilePlacement {
  tileId: string;
  row: number;
  col: number;
}

export interface BackendEvents {
  PlayerJoined: (player: Player) => void;
  PlayerLeft: (playerId: string) => void;
  SeatTaken: (seatId: string, player: Player) => void;
  SeatLeft: (seatId: string) => void;
  ReceiveMessage: (user: string, message: string) => void;
  GameStarted: (gameState: GameState) => void;
  TilesReceived: (tiles: GameTile[]) => void;
  TilesPlaced: (playerId: string, placements: TilePlacement[]) => void;
  TurnChanged: (playerId: string) => void;
  WordSubmitted: (playerId: string, score: number) => void;
  GameEnded: (winnerId: string, finalScores: Record<string, number>) => void;
  PlayerEnteredSeat: (seat: LobbySeatDetails) => void;
  PlayerLeftSeat: (seat: LobbySeatDetails) => void;
}

export interface GameInvokes {
  Join: () => Promise<JoinResponse>;
  SitDown: (seatId: string) => Promise<boolean>;
  LeaveSeat: () => Promise<void>;
  SendMessage: (message: string) => Promise<void>;
  StartGame: () => Promise<void>;
  PlaceTiles: (placements: TilePlacement[]) => Promise<void>;
  SubmitWord: () => Promise<void>;
  DrawNewTiles: () => Promise<void>;
  EnterSeat: (seatId: string) => Promise<boolean>;
}
