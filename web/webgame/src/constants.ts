export const BOARD_SIZE = 15;
export const TILE_SIZE = 35;
export const BOARD_OFFSET_X = 168;
export const BOARD_OFFSET_Y = 80;
export const RACK_Y = 660;

export const JwtStorageKey = "game_jwt_token";
export const PlayerNameStorageKey = "game_player_name";

export const LETTERS = "AABCDEEFGHIIJKLMNOÓPRSŚTUWYZŹŻ";

export const LETTER_VALUES: Record<string, number> = {
  A: 1,
  B: 3,
  C: 2,
  D: 2,
  E: 1,
  F: 5,
  G: 3,
  H: 3,
  I: 1,
  J: 3,
  K: 2,
  L: 2,
  M: 2,
  N: 1,
  O: 1,
  Ó: 5,
  P: 2,
  R: 1,
  S: 1,
  Ś: 5,
  T: 2,
  U: 3,
  W: 1,
  Y: 2,
  Z: 1,
  Ź: 9,
  Ż: 5,
};

// Kolory sepiowe
export const COLORS = {
  darkBg: 0x3e3428, // Ciemny beż
  accentBrown: 0x8b7355, // Brąz
  lightSand: 0xd4c5b9, // Jasny piasek
  boardLight: 0xdcd7ce, // Jasne pola planszy
  boardDark: 0xc9a676, // Ciemniejsze pola planszy
  centerSquare: 0xb8956a, // Centrum
  boardStroke: 0x705d49, // Siatka planszy
  rackBase: 0x7a634a, // Tło stojaka
  rackStroke: 0x544332, // Ramka stojaka
  tileFill: 0xf1e4cf, // Tło płytki
  tileStroke: 0x9f8463, // Ramka płytki
  buttonSuccess: 0x6b8e23, // Oliwkowy zielony - pozytywny, nie jaskrawy
  buttonWarning: 0xcd7f32, // Brązowy pomarańczowy - ostrzezenie
} as const;

export const CHAT_WIDTH = 360;
export const CHAT_HEIGHT = 420;
export const CHAT_GAP = 60; // odstęp między planszą a czatem

export const HUB_URLS = {
  login: "http://localhost:5019/loginHub",
  game: "http://localhost:5019/gameHub",
  chat: "http://localhost:5019/chatHub",
} as const;
