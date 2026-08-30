import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, Observable, throwError } from 'rxjs';
import { ErrorNotificationService } from '../services/error-notification.service';

export const errorInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
): Observable<HttpEvent<unknown>> => {
  const errorNotification = inject(ErrorNotificationService);

  return next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse) {
        // Do not broadcast 401 (handled by authInterceptor) or 409 (handled by offline/idempotency)
        if (err.status !== 401 && err.status !== 409) {
          const problemDetails = err.error as
            { title?: string; detail?: string; error?: string; message?: string } | undefined;
          const title = problemDetails?.title || problemDetails?.error || `HTTP ${err.status}`;
          const detail =
            problemDetails?.detail || problemDetails?.message || err.message || 'Request failed';

          errorNotification.showError({
            title,
            detail,
            statusCode: err.status,
          });
        }
      }
      return throwError(() => err);
    }),
  );
};
