import { V } from '@angular/cdk/keycodes';
import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'boardPosition',
  standalone: true,
  pure: true,
})
export class BoardPositionPipe implements PipeTransform {
  transform(index: number, cols: number, rows: number): CellBoardPosition {
    if (!cols || !rows) {
      return { horizontal: 'center', vertical: 'center' };
    }

    const colIndex = index % cols;
    const rowIndex = Math.floor(index / cols);
    const midCol = (cols - 1) / 2;
    const midRow = (rows - 1) / 2;

    let horizontal: 'left' | 'right' | 'center';
    if (colIndex < midCol) horizontal = 'left';
    else if (colIndex > midCol) horizontal = 'right';
    else horizontal = 'center';

    let vertical: 'top' | 'bottom' | 'center';
    if (rowIndex < midRow) vertical = 'top';
    else if (rowIndex > midRow) vertical = 'bottom';
    else vertical = 'center';

    return { horizontal, vertical };
  }
}

export type CellBoardPosition = {
  horizontal: 'left' | 'right' | 'center';
  vertical: 'top' | 'bottom' | 'center';
};
