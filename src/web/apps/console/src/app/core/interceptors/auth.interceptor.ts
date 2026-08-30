import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, catchError, filter, Observable, switchMap, take, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { I18nService } from '../i18n/i18n.service';

let isRefreshing = false;
const refreshTokenSubject = new BehaviorSubject<string | null>(null);

export const authInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
): Observable<HttpEvent<unknown>> => {
  const authService = inject(AuthService);
  const i18n = inject(I18nService);
  const router = inject(Router);

  const token = authService.accessToken();
  const lang = i18n.currentLang();

  // F13.3: Attach Accept-Language and Authorization
  let headers = req.headers.set(
    'Accept-Language',
    lang === 'zh-CN' ? 'zh-CN,zh;q=0.9' : 'en-NZ,en;q=0.9',
  );

  if (token && !req.headers.has('Authorization')) {
    headers = headers.set('Authorization', `Bearer ${token}`);
  }

  // Ensure non-GET requests have a ClientRequestId
  if (
    ['POST', 'PUT', 'PATCH', 'DELETE'].includes(req.method.toUpperCase()) &&
    !req.headers.has('X-Client-Request-Id')
  ) {
    const reqId =
      typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
        ? crypto.randomUUID()
        : Math.random().toString(36).substring(2);
    headers = headers.set('X-Client-Request-Id', reqId);
    headers = headers.set('ClientRequestId', reqId);
  }

  const modifiedReq = req.clone({ headers });

  return next(modifiedReq).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        // Exclude auth endpoints from refresh loop
        if (
          req.url.includes('/api/auth/login') ||
          req.url.includes('/api/auth/refresh') ||
          req.url.includes('/api/auth/logout')
        ) {
          return throwError(() => error);
        }

        return handle401Error(modifiedReq, next, authService, router);
      }
      return throwError(() => error);
    }),
  );
};

function handle401Error(
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
  authService: AuthService,
  router: Router,
): Observable<HttpEvent<unknown>> {
  if (!isRefreshing) {
    isRefreshing = true;
    refreshTokenSubject.next(null);

    return authService.refreshToken().pipe(
      switchMap((response) => {
        isRefreshing = false;
        refreshTokenSubject.next(response.accessToken);
        return next(
          req.clone({
            headers: req.headers.set('Authorization', `Bearer ${response.accessToken}`),
          }),
        );
      }),
      catchError((err) => {
        isRefreshing = false;
        refreshTokenSubject.next(null);
        authService.clearSession();
        void router.navigate(['/auth/login']);
        return throwError(() => err);
      }),
    );
  }

  return refreshTokenSubject.pipe(
    filter((token): token is string => token !== null),
    take(1),
    switchMap((token) => {
      return next(
        req.clone({
          headers: req.headers.set('Authorization', `Bearer ${token}`),
        }),
      );
    }),
  );
}
