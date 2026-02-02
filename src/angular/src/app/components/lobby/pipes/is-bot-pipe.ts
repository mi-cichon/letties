import { Pipe, type PipeTransform } from '@angular/core';
import { LobbyPlayerDetails } from '../../../api';

@Pipe({
  name: 'isBot',
  standalone: true,
  pure: true,
})
export class IsBotPipe implements PipeTransform {
  transform(playerId: string, players: LobbyPlayerDetails[]): boolean {
    return players.find((x) => x.playerId === playerId)?.isBot ? true : false;
  }
}
