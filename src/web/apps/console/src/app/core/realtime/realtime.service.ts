import { inject, Injectable, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { RealtimeConnectionState, RealtimeMessage } from '../models/realtime.models';

@Injectable({
  providedIn: 'root',
})
export class RealtimeService {
  private readonly authService = inject(AuthService);
  private hubConnection: HubConnection | null = null;

  readonly connectionState = signal<RealtimeConnectionState>('disconnected');
  private readonly invalidationSubject = new Subject<RealtimeMessage>();
  readonly invalidation$ = this.invalidationSubject.asObservable();

  async startConnection(): Promise<void> {
    if (this.hubConnection && this.hubConnection.state === HubConnectionState.Connected) {
      return;
    }

    const token = this.authService.accessToken();
    if (!token) {
      return;
    }

    this.connectionState.set('connecting');

    try {
      this.hubConnection = new HubConnectionBuilder()
        .withUrl('/hubs/realtime', {
          accessTokenFactory: () => this.authService.accessToken() || '',
        })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      this.hubConnection.on('ReceiveInvalidation', (message: RealtimeMessage) => {
        this.invalidationSubject.next(message);
      });

      this.hubConnection.onreconnecting(() => {
        this.connectionState.set('reconnecting');
      });

      this.hubConnection.onreconnected(() => {
        this.connectionState.set('connected');
      });

      this.hubConnection.onclose(() => {
        this.connectionState.set('disconnected');
      });

      await this.hubConnection.start();
      this.connectionState.set('connected');
    } catch {
      this.connectionState.set('disconnected');
    }
  }

  async stopConnection(): Promise<void> {
    if (this.hubConnection) {
      try {
        await this.hubConnection.stop();
      } catch {
        // Ignore stop error
      } finally {
        this.hubConnection = null;
        this.connectionState.set('disconnected');
      }
    }
  }
}
