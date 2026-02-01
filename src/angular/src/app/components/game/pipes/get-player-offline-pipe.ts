import { Pipe, PipeTransform } from '@angular/core';
import { LobbyPlayerDetails } from '../../../api';

@Pipe({
  name: 'getPlayerOffline',
  standalone: true,
  pure: true,
})
export class GetPlayerOfflinePipe implements PipeTransform {
  transform(playerId: string, lobbyPlayers: LobbyPlayerDetails[]): boolean {
    return lobbyPlayers.find((x) => x.playerId == playerId) ? false : true;
  }
}
