import { TestBed } from '@angular/core/testing';
import { computed, signal } from '@angular/core';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../auth/auth.service';
import { AuthUser } from '../models/auth.models';
import { CachedRecord } from '../models/offline.models';
import { IndexedDbService, STORES } from './indexed-db.service';
import { OfflineCacheService } from './offline-cache.service';

describe('OfflineCacheService account isolation', () => {
  const user = (id: string): AuthUser => ({
    id,
    email: `${id}@example.test`,
    displayName: id,
    role: 'Driver',
    locale: 'en-NZ',
  });
  const currentUser = signal<AuthUser | null>(null);
  const authenticated = signal(false);
  const records = new Map<string, CachedRecord<unknown>>();
  const db = {
    get: vi.fn(async (_store: string, key: string) => records.get(key)),
    put: vi.fn(async (_store: string, record: CachedRecord<unknown>) => {
      records.set(record.key, record);
    }),
    getAll: vi.fn(async () => [...records.values()]),
    delete: vi.fn(async (_store: string, key: string) => {
      records.delete(key);
    }),
    clear: vi.fn(),
  };
  let service: OfflineCacheService;
  const login = (id: string) => {
    currentUser.set(user(id));
    authenticated.set(true);
  };

  beforeEach(() => {
    records.clear();
    vi.clearAllMocks();
    currentUser.set(null);
    authenticated.set(false);
    TestBed.configureTestingModule({
      providers: [
        OfflineCacheService,
        { provide: IndexedDbService, useValue: db },
        {
          provide: AuthService,
          useValue: {
            currentUser,
            isAuthenticated: computed(() => authenticated() && currentUser() !== null),
          },
        },
      ],
    });
    service = TestBed.inject(OfflineCacheService);
  });

  it('keeps payslips, tasks, shifts and generic profile data separate for A and B', async () => {
    login('A');
    await service.cacheDriverPayslips([{ netPay: 987 }]);
    await service.cacheDriverTasks([{ id: 'A-task' }]);
    await service.cacheDriverShifts([{ id: 'A-shift' }]);
    await service.cacheData('driver_profile_cache', { phone: 'A-private' });
    login('B');
    expect(await service.getDriverPayslips()).toBeNull();
    expect(await service.getDriverTasks()).toBeNull();
    expect(await service.getDriverShifts()).toBeNull();
    expect(await service.getCachedData('driver_profile_cache')).toBeNull();
    expect(await service.getCacheTimestamp('driver_profile_cache')).toBeNull();
    await service.cacheDriverPayslips([{ netPay: 321 }]);
    expect(await service.getDriverPayslips()).toEqual([{ netPay: 321 }]);
    login('A');
    expect(await service.getDriverPayslips()).toEqual([{ netPay: 987 }]);
    expect(await service.getCachedData('driver_profile_cache')).toEqual({ phone: 'A-private' });
    expect(await service.getCacheTimestamp('driver_profile_cache')).toEqual(expect.any(String));
    expect([...records.keys()]).toContain('user:A:driver_payslips_cache');
    expect([...records.keys()]).toContain('user:B:driver_payslips_cache');
  });

  it('ignores legacy shared entries without assigning an unknown owner', async () => {
    records.set('driver_payslips_cache', {
      key: 'driver_payslips_cache',
      data: [{ netPay: 999 }],
      cachedAt: '2026-09-09',
    });
    login('A');
    expect(await service.getDriverPayslips()).toBeNull();
    expect(await service.getCacheTimestamp('driver_payslips_cache')).toBeNull();
    expect(db.get).not.toHaveBeenCalledWith(STORES.OFFLINE_CACHE, 'driver_payslips_cache');
    expect(db.put).not.toHaveBeenCalled();
  });

  it('does not access the cache without an authenticated owner', async () => {
    currentUser.set(user('A')); // Stale stored user without a valid session is insufficient.
    await service.cacheDriverPayslips([{ netPay: 999 }]);
    expect(await service.getDriverPayslips()).toBeNull();
    expect(await service.getCacheTimestamp('driver_profile_cache')).toBeNull();
    await service.clearAllCache();
    expect(db.get).not.toHaveBeenCalled();
    expect(db.put).not.toHaveBeenCalled();
    expect(db.getAll).not.toHaveBeenCalled();
  });

  it.each(['logout', 'switch', 'new-session'] as const)(
    'discards a pending read on %s',
    async (change) => {
      login('A');
      let resolve!: (record: CachedRecord<unknown>) => void;
      db.get.mockReturnValueOnce(
        new Promise((complete) => {
          resolve = complete;
        }),
      );
      const pending = service.getDriverPayslips();
      if (change === 'logout') {
        authenticated.set(false);
        currentUser.set(null);
      } else login(change === 'switch' ? 'B' : 'A');
      resolve({
        key: 'user:A:driver_payslips_cache',
        data: [{ netPay: 999 }],
        cachedAt: '2026-09-09',
      });
      expect(await pending).toBeNull();
    },
  );

  it('does not return a previous account timestamp after a switch', async () => {
    login('A');
    let resolve!: (record: CachedRecord<unknown>) => void;
    db.get.mockReturnValueOnce(
      new Promise((complete) => {
        resolve = complete;
      }),
    );
    const pending = service.getCacheTimestamp('driver_profile_cache');
    login('B');
    resolve({ key: 'user:A:driver_profile_cache', data: {}, cachedAt: '2026-09-09' });
    expect(await pending).toBeNull();
  });

  it('writes to the captured account even when persistence finishes after a switch', async () => {
    login('A');
    let resolve!: () => void;
    db.put.mockImplementationOnce(async (_store, record) => {
      await new Promise<void>((complete) => {
        resolve = complete;
      });
      records.set(record.key, record);
    });
    const pending = service.cacheDriverPayslips([{ netPay: 999 }]);
    login('B');
    resolve();
    await pending;
    expect(await service.getDriverPayslips()).toBeNull();
    expect(records.has('user:B:driver_payslips_cache')).toBe(false);
  });

  it('returns no private data when storage is unavailable or returns a mismatched key', async () => {
    login('A');
    db.get.mockRejectedValueOnce(new Error('Unavailable'));
    expect(await service.getDriverPayslips()).toBeNull();
    db.get.mockResolvedValueOnce({
      key: 'user:B:driver_payslips_cache',
      data: [{ netPay: 999 }],
      cachedAt: '2026-09-09',
    });
    expect(await service.getDriverPayslips()).toBeNull();
  });

  it('clears only the current account cache without touching the queue or other accounts', async () => {
    login('A');
    await service.cacheDriverPayslips([{ netPay: 987 }]);
    login('B');
    await service.cacheDriverPayslips([{ netPay: 321 }]);
    await service.clearAllCache();
    expect(await service.getDriverPayslips()).toBeNull();
    login('A');
    expect(await service.getDriverPayslips()).toEqual([{ netPay: 987 }]);
    expect(db.clear).not.toHaveBeenCalled();
    expect(db.delete).toHaveBeenCalledWith(STORES.OFFLINE_CACHE, 'user:B:driver_payslips_cache');
  });
});
