import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { ErrorNotificationService } from '../services/error-notification.service';
import { errorInterceptor } from './error.interceptor';

describe('errorInterceptor (W26 R2 422 ProblemDetails Fallback)', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let errorService: ErrorNotificationService;

  beforeEach(() => {
    TestBed.resetTestingModule();

    TestBed.configureTestingModule({
      providers: [
        ErrorNotificationService,
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    errorService = TestBed.inject(ErrorNotificationService);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('AC 4: when backend returns 422 ProblemDetails, captures understandable domain detail rather than raw HTTP error string', () => {
    httpClient
      .post('/api/dispatch/tasks/TSK-20260920-E2E337/status', { status: 'IN_PROGRESS' })
      .subscribe({
        next: () => expect.unreachable('Should have failed with 422'),
        error: () => {},
      });

    const req = httpMock.expectOne('/api/dispatch/tasks/TSK-20260920-E2E337/status');
    const problemDetails = {
      type: 'https://tools.ietf.org/html/rfc9110#section-15.5.21',
      title: 'invalid_task_transition',
      status: 422,
      detail:
        "Cannot transition task from 'Assigned' to 'InProgress'. Task must be 'Acknowledged' first.",
    };

    req.flush(problemDetails, { status: 422, statusText: 'Unprocessable Entity' });

    const latest = errorService.latestError();
    expect(latest).not.toBeNull();
    expect(latest?.statusCode).toBe(422);
    expect(latest?.title).toBe('invalid_task_transition');
    expect(latest?.detail).toBe(
      "Cannot transition task from 'Assigned' to 'InProgress'. Task must be 'Acknowledged' first.",
    );

    // Verify it is NOT the raw Angular HttpErrorResponse string
    expect(latest?.detail).not.toContain('Http failure response');
  });

  it('AC 4: when backend returns 422 without detail, provides meaningful fallback key', () => {
    httpClient.post('/api/dispatch/tasks/TSK-001/status', {}).subscribe({
      error: () => {},
    });

    const req = httpMock.expectOne('/api/dispatch/tasks/TSK-001/status');
    req.flush({}, { status: 422, statusText: 'Unprocessable Entity' });

    const latest = errorService.latestError();
    expect(latest).not.toBeNull();
    expect(latest?.statusCode).toBe(422);
    expect(latest?.title).toBe('COMMON.INVALID_OPERATION');
    expect(latest?.detail).toBe('COMMON.INVALID_STATE_TRANSITION');
  });

  it('does not broadcast 401 or 409 errors through global error notifier', () => {
    httpClient.get('/api/test-401').subscribe({ error: () => {} });
    httpMock
      .expectOne('/api/test-401')
      .flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });
    expect(errorService.latestError()).toBeNull();

    httpClient.get('/api/test-409').subscribe({ error: () => {} });
    httpMock.expectOne('/api/test-409').flush('Conflict', { status: 409, statusText: 'Conflict' });
    expect(errorService.latestError()).toBeNull();
  });
});
