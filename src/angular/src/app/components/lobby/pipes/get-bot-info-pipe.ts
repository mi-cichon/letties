import { Pipe, type PipeTransform } from '@angular/core';
import { BotDifficulty, LobbyPlayerDetails } from '../../../api';

@Pipe({
  name: 'getBotInfo',
  standalone: true,
  pure: true,
})
export class GetBotInfoPipe implements PipeTransform {
  transform(playerId: string, players: LobbyPlayerDetails[]): BotInfo {
    const player = players.find((x) => x.playerId === playerId);

    if (!player) {
      return { isBot: false };
    }

    let difficultyText: string | undefined;

    switch (player.botDifficulty) {
      case BotDifficulty.Easy:
        difficultyText = '⭐';
        break;
      case BotDifficulty.Medium:
        difficultyText = '⭐⭐';
        break;
      case BotDifficulty.Hard:
        difficultyText = '⭐⭐⭐';
        break;
    }

    return {
      isBot: player.isBot!,
      difficulty: player.botDifficulty,
      difficultyText: difficultyText,
    };
  }
}

export type BotInfo = {
  isBot: boolean;
  difficulty?: BotDifficulty;
  difficultyText?: string;
};
