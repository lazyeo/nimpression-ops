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
        if (err.status !== 401 && err.status !== 409) {
          const problemDetails = err.error as
            | {
                title?: string;
                detail?: string;
                error?: string;
                message?: string;
                errors?: Record<string, string[]>;
              }
            | undefined;

          let title = problemDetails?.title || problemDetails?.error;
          let detail = problemDetails?.detail || problemDetails?.message;

          if (!title) {
            title = err.status === 422 ? 'COMMON.INVALID_OPERATION' : `HTTP ${err.status}`;
          }

          if (!detail) {
            if (problemDetails?.errors && Object.keys(problemDetails.errors).length > 0) {
              detail = Object.values(problemDetails.errors).flat().join('; ');
            } else if (err.status === 422) {
              detail = 'COMMON.INVALID_STATE_TRANSITION';
            } else if (typeof err.error === 'string') {
              detail = err.error;
            } else {
              detail = err.statusText || 'Request failed';
            }
          }

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
