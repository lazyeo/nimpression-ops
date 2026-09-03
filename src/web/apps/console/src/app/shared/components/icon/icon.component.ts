import { Component, ChangeDetectionStrategy, input } from '@angular/core';

@Component({
  selector: 'nim-icon',
  standalone: true,
  templateUrl: './icon.component.html',
  styleUrls: ['./icon.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IconComponent {
  readonly name = input.required<string>();
  readonly size = input<number>(20);
}
