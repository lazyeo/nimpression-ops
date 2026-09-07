import { inject, Injectable, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  IRetryPolicy,
  LogLevel,
  RetryContext,
} from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { RealtimeConnectionState, RealtimeMessage } from '../models/realtime.models';

/**
 * Custom retry policy implementing infinite exponential backoff with jitter and a maximum delay cap.
 *
 * Rationale:
 * 1. Default SignalR policy gives up after 4 attempts (~42s total). Deployments / server restarts
 *    frequently exceed 42s, causing active client sessions to permanently disconnect.
 * 2. Infinite retries ensure that open browser tabs automatically reconnect after any deployment duration.
 * 3. Exponential backoff (0s, 2s, 5s, 10s, 20s, capped at 30s) prevents aggressive server hammering.
 * 4. Jitter (+/- 20%) prevents "thundering herd" reconnection storms when multiple clients
 *    reconnect simultaneously following a server restart.
 */
export class RealtimeInfiniteBackoffRetryPolicy implements IRetryPolicy {
  private readonly baseDelays = [0, 2000, 5000, 10000, 20000];
  private readonly maxDelayMs = 30000;

  nextRetryDelayInMilliseconds(retryContext: RetryContext): number {
    const count = retryContext.previousRetryCount;
    const baseDelay = count < this.baseDelays.length ? this.baseDelays[count] : this.maxDelayMs;

    if (baseDelay === 0) {
      return 0;
    }

    // Jitter: +/- 20%
    const jitter = (Math.random() * 0.4 - 0.2) * baseDelay;
    return Math.round(baseDelay + jitter);
  }
}

@Injectable({
  providedIn: 'root',
})
export class RealtimeService {
  private readonly authService = inject(AuthService);
  private hubConnection: HubConnection | null = null;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private initialRetryCount = 0;
  private isExplicitlyStopped = false;

  readonly connectionState = signal<RealtimeConnectionState>('disconnected');
  private readonly invalidationSubject = new Subject<RealtimeMessage>();
  readonly invalidation$ = this.invalidationSubject.asObservable();

  async startConnection(): Promise<void> {
    this.isExplicitlyStopped = false;

    if (this.hubConnection && this.hubConnection.state === HubConnectionState.Connected) {
      return;
    }

    const token = this.authService.accessToken();
    if (!token) {
      this.connectionState.set('disconnected');
      return;
    }

    this.connectionState.set('connecting');

    try {
      if (!this.hubConnection) {
        this.hubConnection = new HubConnectionBuilder()
          .withUrl('/hubs/realtime', {
            accessTokenFactory: () => this.authService.accessToken() || '',
          })
          .withAutomaticReconnect(new RealtimeInfiniteBackoffRetryPolicy())
          .configureLogging(LogLevel.Warning)
          .build();

        this.hubConnection.on('ReceiveInvalidation', (message: RealtimeMessage) => {
          this.invalidationSubject.next(message);
        });

        this.hubConnection.onreconnecting(() => {
          this.connectionState.set('reconnecting');
        });

        this.hubConnection.onreconnected(() => {
          this.initialRetryCount = 0;
          this.connectionState.set('connected');
          // Emit reconnect invalidation so subscriber components refresh and catch up
          this.invalidationSubject.next({
            kind: 'realtime.reconnected',
            entityId: '',
            occurredAt: new Date().toISOString(),
          });
        });

        this.hubConnection.onclose(() => {
          this.connectionState.set('disconnected');
          if (!this.isExplicitlyStopped && this.authService.accessToken()) {
            this.scheduleInitialConnectRetry();
          }
        });
      }

      await this.hubConnection.start();
      this.initialRetryCount = 0;
      this.connectionState.set('connected');
    } catch {
      this.connectionState.set('disconnected');
      if (!this.isExplicitlyStopped && this.authService.accessToken()) {
        this.scheduleInitialConnectRetry();
      }
    }
  }

  private scheduleInitialConnectRetry(): void {
    if (this.reconnectTimer || this.isExplicitlyStopped) {
      return;
    }

    const baseDelays = [1000, 2000, 5000, 10000, 20000];
    const baseDelay =
      this.initialRetryCount < baseDelays.length ? baseDelays[this.initialRetryCount] : 30000;
    const jitter = (Math.random() * 0.4 - 0.2) * baseDelay;
    const delay = Math.round(baseDelay + jitter);

    this.initialRetryCount++;
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      if (!this.isExplicitlyStopped && this.authService.accessToken()) {
        void this.startConnection();
      }
    }, delay);
  }

  async stopConnection(): Promise<void> {
    this.isExplicitlyStopped = true;
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
    this.initialRetryCount = 0;

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
