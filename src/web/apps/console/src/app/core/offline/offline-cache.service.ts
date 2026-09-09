import { inject, Injectable } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { AuthUser } from '../models/auth.models';
import { CachedRecord } from '../models/offline.models';
import { IndexedDbService, STORES } from './indexed-db.service';

const CACHE_KEYS = {
  DRIVER_TASKS: 'driver_tasks_cache',
  DRIVER_PAYSLIPS: 'driver_payslips_cache',
  DRIVER_SHIFTS: 'driver_shifts_cache',
  DRIVER_PROFILE: 'driver_profile_cache',
} as const;

@Injectable({
  providedIn: 'root',
})
export class OfflineCacheService {
  private readonly indexedDb = inject(IndexedDbService);
  private readonly auth = inject(AuthService);

  private currentOwner(): AuthUser | null {
    const user = this.auth.currentUser();
    return this.auth.isAuthenticated() && typeof user?.id === 'string' && user.id.trim()
      ? user
      : null;
  }

  private ownerPrefix(owner: AuthUser): string {
    return `user:${encodeURIComponent(owner.id)}:`;
  }

  private scopedKey(owner: AuthUser, key: string): string {
    return this.ownerPrefix(owner) + encodeURIComponent(key);
  }

  async cacheData<T>(key: string, data: T): Promise<void> {
    const owner = this.currentOwner();
    if (!owner) return;
    const record: CachedRecord<T> = {
      key: this.scopedKey(owner, key),
      data,
      cachedAt: new Date().toISOString(),
    };
    await this.indexedDb.put(STORES.OFFLINE_CACHE, record);
  }

  getCachedData<T>(key: string): Promise<T | null> {
    return this.readRecord<T, T>(key, (record) => record.data);
  }

  getCacheTimestamp(key: string): Promise<string | null> {
    return this.readRecord<unknown, string>(key, (record) => record.cachedAt);
  }

  private async readRecord<T, TResult>(
    key: string,
    select: (record: CachedRecord<T>) => TResult,
  ): Promise<TResult | null> {
    const owner = this.currentOwner();
    if (!owner) return null;
    const scopedKey = this.scopedKey(owner, key);
    try {
      const record = await this.indexedDb.get<CachedRecord<T>>(STORES.OFFLINE_CACHE, scopedKey);
      // Session identity, not just an account id: a stale read must not survive a logout/login.
      if (this.currentOwner() !== owner || record?.key !== scopedKey) return null;
      return select(record);
    } catch {
      // An unavailable private cache must never fall back to legacy shared entries.
      return null;
    }
  }

  // Specialized cache methods for Driver shell
  async cacheDriverTasks<T>(tasks: T[]): Promise<void> {
    await this.cacheData(CACHE_KEYS.DRIVER_TASKS, tasks);
  }

  getDriverTasks<T>(): Promise<T[] | null> {
    return this.getCachedData<T[]>(CACHE_KEYS.DRIVER_TASKS);
  }

  async cacheDriverPayslips<T>(payslips: T[]): Promise<void> {
    await this.cacheData(CACHE_KEYS.DRIVER_PAYSLIPS, payslips);
  }

  getDriverPayslips<T>(): Promise<T[] | null> {
    return this.getCachedData<T[]>(CACHE_KEYS.DRIVER_PAYSLIPS);
  }

  async cacheDriverShifts<T>(shifts: T[]): Promise<void> {
    await this.cacheData(CACHE_KEYS.DRIVER_SHIFTS, shifts);
  }

  getDriverShifts<T>(): Promise<T[] | null> {
    return this.getCachedData<T[]>(CACHE_KEYS.DRIVER_SHIFTS);
  }

  async clearAllCache(): Promise<void> {
    const owner = this.currentOwner();
    if (!owner) return;
    const records = await this.indexedDb.getAll<CachedRecord<unknown>>(STORES.OFFLINE_CACHE);
    if (this.currentOwner() !== owner) return;
    const prefix = this.ownerPrefix(owner);
    await Promise.all(
      records
        .filter((record) => record.key.startsWith(prefix))
        .map((record) => this.indexedDb.delete(STORES.OFFLINE_CACHE, record.key)),
    );
  }
}
