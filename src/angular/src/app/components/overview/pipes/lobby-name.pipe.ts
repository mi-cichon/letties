import { Pipe, PipeTransform } from '@angular/core';
import { lobbyColors, lobbyIcons } from './lobby-names';

@Pipe({
  name: 'lobbyName',
  standalone: true,
  pure: true,
})
export class LobbyNamePipe implements PipeTransform {
  transform(id: string | undefined | null): string {
    if (!id || !id.includes('-')) return 'Unknown Room';
    const parts = id.split('-');
    const randomPart = parts[parts.length - 1];

    const seedColor = parseInt(randomPart.substring(0, 6), 16);
    const seedIcon = parseInt(randomPart.substring(6, 12), 16);

    const color = lobbyColors[seedColor % lobbyColors.length];
    const icon = lobbyIcons[seedIcon % lobbyIcons.length];

    return `${color} ${icon}`;
  }
}
