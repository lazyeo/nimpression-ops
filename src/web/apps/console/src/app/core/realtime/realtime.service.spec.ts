import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { RealtimeService, RealtimeInfiniteBackoffRetryPolicy } from './realtime.service';
import { AuthService } from '../auth/auth.service';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

describe('RealtimeInfiniteBackoffRetryPolicy', () => {
  const policy = new RealtimeInfiniteBackoffRetryPolicy();

  it('returns 0ms for the immediate 0th retry attempt', () => {
    const delay = policy.nextRetryDelayInMilliseconds({
      previousRetryCount: 0,
      elapsedMilliseconds: 0,
      retryReason: new Error('Network lost'),
    });
    expect(delay).toBe(0);
  });

  it('applies backoff ladder with jitter for attempts 1 through 4', () => {
    // Attempt 1: ~2000ms (+/- 20% -> 1600ms to 2400ms)
    const delay1 = policy.nextRetryDelayInMilliseconds({
      previousRetryCount: 1,
      elapsedMilliseconds: 2000,
      retryReason: new Error('Network lost'),
    });
    expect(delay1).toBeGreaterThanOrEqual(1600);
    expect(delay1).toBeLessThanOrEqual(2400);

    // Attempt 2: ~5000ms (+/- 20% -> 4000ms to 6000ms)
    const delay2 = policy.nextRetryDelayInMilliseconds({
      previousRetryCount: 2,
      elapsedMilliseconds: 7000,
      retryReason: new Error('Network lost'),
    });
    expect(delay2).toBeGreaterThanOrEqual(4000);
    expect(delay2).toBeLessThanOrEqual(6000);

    // Attempt 3: ~10000ms (+/- 20% -> 8000ms to 12000ms)
    const delay3 = policy.nextRetryDelayInMilliseconds({
      previousRetryCount: 3,
      elapsedMilliseconds: 17000,
      retryReason: new Error('Network lost'),
    });
    expect(delay3).toBeGreaterThanOrEqual(8000);
    expect(delay3).toBeLessThanOrEqual(12000);

    // Attempt 4: ~20000ms (+/- 20% -> 16000ms to 24000ms)
    const delay4 = policy.nextRetryDelayInMilliseconds({
      previousRetryCount: 4,
      elapsedMilliseconds: 37000,
      retryReason: new Error('Network lost'),
    });
    expect(delay4).toBeGreaterThanOrEqual(16000);
    expect(delay4).toBeLessThanOrEqual(24000);
  });

  it('caps delay at 30000ms (+/- 20%) and never gives up for large retry counts', () => {
    // Attempt 5
    const delay5 = policy.nextRetryDelayInMilliseconds({
      previousRetryCount: 5,
      elapsedMilliseconds: 67000,
      retryReason: new Error('Server restart in progress'),
    });
    expect(delay5).toBeGreaterThanOrEqual(24000);
    expect(delay5).toBeLessThanOrEqual(36000);

    // Attempt 100 (e.g. after a lengthy deployment)
    const delay100 = policy.nextRetryDelayInMilliseconds({
      previousRetryCount: 100,
      elapsedMilliseconds: 3000000,
      retryReason: new Error('Server restart in progress'),
    });
    expect(delay100).not.toBeNull();
    expect(delay100).toBeGreaterThanOrEqual(24000);
    expect(delay100).toBeLessThanOrEqual(36000);
  });
});

describe('RealtimeService', () => {
  let service: RealtimeService;
  let authService: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [RealtimeService, AuthService, provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(RealtimeService);
    authService = TestBed.inject(AuthService);
  });

  afterEach(async () => {
    await service.stopConnection();
  });

  it('initializes in disconnected state', () => {
    expect(service.connectionState()).toBe('disconnected');
  });

  it('does not attempt connection if unauthenticated', async () => {
    authService.clearSession();
    await service.startConnection();
    expect(service.connectionState()).toBe('disconnected');
  });

  it('sets state to disconnected on stopConnection', async () => {
    await service.stopConnection();
    expect(service.connectionState()).toBe('disconnected');
  });
});
