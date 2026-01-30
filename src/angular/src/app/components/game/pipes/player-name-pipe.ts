import { Pipe, type PipeTransform } from '@angular/core';
import { LobbyPlayerDetails } from '../../../api';

@Pipe({
  name: 'playerName',
  pure: true,
  standalone: true,
})
export class PlayerNamePipe implements PipeTransform {
  transform(playerId: string, players: LobbyPlayerDetails[]): string {
    return players.find((x) => x.playerId == playerId)?.playerName ?? '';
  }
}
