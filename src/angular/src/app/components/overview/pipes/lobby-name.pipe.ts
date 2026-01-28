import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'lobbyName',
  standalone: true,
  pure: true,
})
export class LobbyNamePipe implements PipeTransform {
  private readonly colors = [
    'Red',
    'Blue',
    'Gold',
    'Silver',
    'Jade',
    'Neon',
    'Royal',
    'Dark',
    'Amber',
    'Coral',
  ];
  private readonly icons = [
    'Falcon',
    'Raven',
    'Knight',
    'Bishop',
    'Ace',
    'Crown',
    'Star',
    'Wolf',
    'Tiger',
    'Bear',
  ];

  transform(id: string | undefined | null): string {
    if (!id || !id.includes('-')) return 'Unknown Room';
    const parts = id.split('-');
    const randomPart = parts[parts.length - 1];

    const seedColor = parseInt(randomPart.substring(0, 6), 16);
    const seedIcon = parseInt(randomPart.substring(6, 12), 16);

    const color = this.colors[seedColor % this.colors.length];
    const icon = this.icons[seedIcon % this.icons.length];

    return `${color} ${icon}`;
  }
}
