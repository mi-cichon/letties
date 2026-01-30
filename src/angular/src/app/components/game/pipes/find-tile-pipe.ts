import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'findTile',
  standalone: true,
  pure: true,
})
export class FindTilePipe implements PipeTransform {
  /**
   * @param tiles Tablica kafelków z myHand
   * @param tileId ID kafelka, którego szukamy
   */
  transform(tiles: any[] | undefined | null, tileId: string): any | undefined {
    if (!tiles || !tileId) return undefined;
    return tiles.find((t) => t.tileId === tileId);
  }
}
