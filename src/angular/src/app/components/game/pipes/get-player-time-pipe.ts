import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'getPlayerTime',
  standalone: true,
  pure: true,
})
export class GetPlayerTimePipe implements PipeTransform {
  transform(playerId: string, playerTimes: Map<string, string>): string {
    return playerTimes.get(playerId) ?? '';
  }
}
