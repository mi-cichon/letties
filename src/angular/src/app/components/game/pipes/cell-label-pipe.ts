import { Pipe, PipeTransform } from '@angular/core';
import { LetterCellType } from '../../../api';

@Pipe({
  name: 'cellLabel',
  standalone: true,
})
export class CellLabelPipe implements PipeTransform {
  transform(type: LetterCellType): string {
    switch (type) {
      case 'Center':
        return '★';
      case 'DoubleLetter':
        return '2L';
      case 'TripleLetter':
        return '3L';
      case 'DoubleWord':
        return '2W';
      case 'TripleWord':
        return '3W';
      default:
        return '';
    }
  }
}
