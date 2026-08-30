import { Injectable, signal } from '@angular/core';

export interface AppError {
  id: string;
  title: string;
  detail: string;
  statusCode?: number;
  timestamp: string;
}

@Injectable({
  providedIn: 'root',
})
export class ErrorNotificationService {
  readonly activeErrors = signal<AppError[]>([]);
  readonly latestError = signal<AppError | null>(null);

  showError(error: { title?: string; detail?: string; statusCode?: number }): void {
    const appError: AppError = {
      id: Math.random().toString(36).substring(2, 9),
      title: error.title || 'COMMON.ERROR',
      detail: error.detail || 'An unexpected error occurred',
      statusCode: error.statusCode,
      timestamp: new Date().toISOString(),
    };

    this.latestError.set(appError);
    this.activeErrors.update((list) => [appError, ...list.slice(0, 4)]);
  }

  dismissError(id: string): void {
    this.activeErrors.update((list) => list.filter((e) => e.id !== id));
    if (this.latestError()?.id === id) {
      this.latestError.set(null);
    }
  }

  clearAll(): void {
    this.activeErrors.set([]);
    this.latestError.set(null);
  }
}
