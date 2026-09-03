import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { NotificationsComponent } from './notifications.component';
import { NotificationService } from './services/notification.service';
import { AuthService } from '../../../core/auth/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';

describe('NotificationsComponent', () => {
  let component: NotificationsComponent;
  let fixture: ComponentFixture<NotificationsComponent>;

  beforeEach(async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [NotificationsComponent],
      providers: [
        NotificationService,
        AuthService,
        I18nService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(NotificationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('initializes with default logs tab and switches tabs correctly', () => {
    expect(component.activeTab()).toBe('logs');

    component.setActiveTab('templates');
    expect(component.activeTab()).toBe('templates');

    component.setActiveTab('partners');
    expect(component.activeTab()).toBe('partners');

    component.setActiveTab('compliance');
    expect(component.activeTab()).toBe('compliance');
  });
});
