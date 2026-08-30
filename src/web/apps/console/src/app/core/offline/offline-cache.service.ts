import { inject, Injectable } from '@angular/core';
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

  async cacheData<T>(key: string, data: T): Promise<void> {
    const record: CachedRecord<T> = {
      key,
      data,
      cachedAt: new Date().toISOString(),
    };
    await this.indexedDb.put(STORES.OFFLINE_CACHE, record);
  }

  async getCachedData<T>(key: string): Promise<T | null> {
    const record = await this.indexedDb.get<CachedRecord<T>>(STORES.OFFLINE_CACHE, key);
    return record ? record.data : null;
  }

  async getCacheTimestamp(key: string): Promise<string | null> {
    const record = await this.indexedDb.get<CachedRecord<unknown>>(STORES.OFFLINE_CACHE, key);
    return record ? record.cachedAt : null;
  }

  // Specialized cache methods for Driver shell
  async cacheDriverTasks<T>(tasks: T[]): Promise<void> {
    await this.cacheData(CACHE_KEYS.DRIVER_TASKS, tasks);
  }

  async getDriverTasks<T>(): Promise<T[] | null> {
    return this.getCachedData<T[]>(CACHE_KEYS.DRIVER_TASKS);
  }

  async cacheDriverPayslips<T>(payslips: T[]): Promise<void> {
    await this.cacheData(CACHE_KEYS.DRIVER_PAYSLIPS, payslips);
  }

  async getDriverPayslips<T>(): Promise<T[] | null> {
    return this.getCachedData<T[]>(CACHE_KEYS.DRIVER_PAYSLIPS);
  }

  async cacheDriverShifts<T>(shifts: T[]): Promise<void> {
    await this.cacheData(CACHE_KEYS.DRIVER_SHIFTS, shifts);
  }

  async getDriverShifts<T>(): Promise<T[] | null> {
    return this.getCachedData<T[]>(CACHE_KEYS.DRIVER_SHIFTS);
  }

  async clearAllCache(): Promise<void> {
    await this.indexedDb.clear(STORES.OFFLINE_CACHE);
  }
}
