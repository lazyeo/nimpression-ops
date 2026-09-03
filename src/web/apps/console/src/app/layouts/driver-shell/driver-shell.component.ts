import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { I18nPipe } from '../../core/i18n/i18n.pipe';
import { RealtimeService } from '../../core/realtime/realtime.service';
import { OfflineStatusComponent } from '../../core/offline/offline-status.component';
import { SupportedLang } from '../../core/models/i18n.models';
import { IconComponent } from '../../shared/components/icon/icon.component';

interface DriverNavItem {
  path: string;
  labelKey: string;
  icon: string;
}

@Component({
  selector: 'nim-driver-shell',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    I18nPipe,
    OfflineStatusComponent,
    IconComponent,
  ],
  templateUrl: './driver-shell.component.html',
  styleUrl: './driver-shell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DriverShellComponent implements OnInit {
  readonly authService = inject(AuthService);
  readonly i18n = inject(I18nService);
  readonly realtime = inject(RealtimeService);
  private readonly router = inject(Router);

  readonly bottomNavItems: DriverNavItem[] = [
    { path: '/driver/tasks', labelKey: 'NAV.TASKS', icon: 'tasks' },
    { path: '/driver/shifts', labelKey: 'NAV.SHIFTS', icon: 'shifts' },
    { path: '/driver/payslips', labelKey: 'NAV.PAYSLIPS', icon: 'payslips' },
    { path: '/driver/profile', labelKey: 'NAV.PROFILE', icon: 'user' },
  ];

  ngOnInit(): void {
    void this.realtime.startConnection();
  }

  get currentLang(): SupportedLang {
    return this.i18n.currentLang();
  }

  toggleLanguage(): void {
    const next: SupportedLang = this.currentLang === 'en-NZ' ? 'zh-CN' : 'en-NZ';
    this.authService.updateUserLocale(next).subscribe();
  }

  logout(): void {
    void this.realtime.stopConnection();
    this.authService.logout().subscribe();
  }
}
