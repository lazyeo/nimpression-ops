import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { UpdateNotificationComponent } from './core/sw/update-notification.component';

@Component({
  selector: 'nim-root',
  imports: [RouterOutlet, UpdateNotificationComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {}
