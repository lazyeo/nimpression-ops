import { Pipe, PipeTransform } from '@angular/core';
import { toScreamingSnake } from '../utils/case.utils';

@Pipe({
  name: 'screamingSnake',
  standalone: true,
  pure: true,
})
export class ScreamingSnakePipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return toScreamingSnake(value);
  }
}
