import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { I18nService } from '../i18n/i18n.service';
import { SwUpdateService } from './sw-update.service';
import { UpdateNotificationComponent } from './update-notification.component';

describe('UpdateNotificationComponent (W26 AC 2 & AC 3 UI Verification)', () => {
  let fixture: ComponentFixture<UpdateNotificationComponent>;
  let component: UpdateNotificationComponent;
  let updateAvailableSignal: ReturnType<typeof signal<boolean>>;
  let isUnrecoverableSignal: ReturnType<typeof signal<boolean>>;
  let mockSwService: any;

  beforeEach(async () => {
    TestBed.resetTestingModule();

    updateAvailableSignal = signal<boolean>(false);
    isUnrecoverableSignal = signal<boolean>(false);

    mockSwService = {
      updateAvailable: updateAvailableSignal,
      isUnrecoverable: isUnrecoverableSignal,
      activateAndReload: vi.fn().mockResolvedValue(undefined),
      dismissUpdateNotification: vi.fn().mockImplementation(() => {
        updateAvailableSignal.set(false);
      }),
      reloadApp: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [UpdateNotificationComponent],
      providers: [
        I18nService,
        {
          provide: SwUpdateService,
          useValue: mockSwService,
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(UpdateNotificationComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders nothing when no update is available and state is normal', () => {
    const rootEl = fixture.nativeElement as HTMLElement;
    expect(rootEl.querySelector('.update-banner')).toBeNull();
    expect(rootEl.querySelector('.unrecoverable-banner')).toBeNull();
  });

  it('AC 2: when VERSION_READY is detected, UI renders update banner with actions instead of silent reload', () => {
    updateAvailableSignal.set(true);
    fixture.detectChanges();

    const rootEl = fixture.nativeElement as HTMLElement;
    const banner = rootEl.querySelector('.update-banner');
    expect(banner).not.toBeNull();

    const updateBtn = banner?.querySelector('.btn-update') as HTMLButtonElement;
    const dismissBtn = banner?.querySelector('.btn-dismiss') as HTMLButtonElement;

    expect(updateBtn).not.toBeNull();
    expect(dismissBtn).not.toBeNull();

    // Clicking update triggers activateAndReload
    updateBtn.click();
    expect(mockSwService.activateAndReload).toHaveBeenCalledTimes(1);

    // Clicking dismiss triggers dismissUpdateNotification
    dismissBtn.click();
    expect(mockSwService.dismissUpdateNotification).toHaveBeenCalledTimes(1);
  });

  it('AC 3: when unrecoverable state occurs, UI renders unrecoverable banner prompting reload', () => {
    isUnrecoverableSignal.set(true);
    fixture.detectChanges();

    const rootEl = fixture.nativeElement as HTMLElement;
    const banner = rootEl.querySelector('.unrecoverable-banner');
    expect(banner).not.toBeNull();

    const reloadBtn = banner?.querySelector('.btn-reload') as HTMLButtonElement;
    expect(reloadBtn).not.toBeNull();

    // Clicking reload triggers reloadApp
    reloadBtn.click();
    expect(mockSwService.reloadApp).toHaveBeenCalledTimes(1);
  });
});
