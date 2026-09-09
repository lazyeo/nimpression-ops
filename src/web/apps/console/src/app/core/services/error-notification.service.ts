import { HttpErrorResponse } from '@angular/common/http';
import { resolveUserFacingError } from '../errors/user-facing-error';
import { Injectable, signal } from '@angular/core';

export interface AppError {
  id: string;
  code: string;
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

  showError(error: unknown): void {
    const safe = resolveUserFacingError(error);
    const appError: AppError = {
      id: Math.random().toString(36).substring(2, 9),
      title: safe.titleKey,
      detail: safe.messageKey,
      code: safe.code,
      statusCode: error instanceof HttpErrorResponse ? error.status : undefined,
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
