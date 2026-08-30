import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { DriverShiftsComponent } from './driver-shifts.component';
import { OfflineCacheService } from '../../../core/offline/offline-cache.service';
import { OfflineQueueService } from '../../../core/offline/offline-queue.service';
import { I18nService } from '../../../core/i18n/i18n.service';

describe('DriverShiftsComponent (Clock in/out & Shifts)', () => {
  let component: DriverShiftsComponent;

  beforeEach(async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [DriverShiftsComponent],
      providers: [
        I18nService,
        OfflineCacheService,
        OfflineQueueService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(DriverShiftsComponent);
    component = fixture.componentInstance;
  });

  it('handles clock in state update', async () => {
    expect(component.currentShift().status).toBe('NOT_STARTED');
    await component.clockIn();
    expect(component.currentShift().status).toBe('ACTIVE');
  });
});
