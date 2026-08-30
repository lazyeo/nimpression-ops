import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { I18nPipe } from '../../core/i18n/i18n.pipe';
import { RealtimeService } from '../../core/realtime/realtime.service';
import { OfflineStatusComponent } from '../../core/offline/offline-status.component';
import { SupportedLang } from '../../core/models/i18n.models';

interface NavItem {
  path: string;
  labelKey: string;
  icon: string;
}

@Component({
  selector: 'nim-admin-shell',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    I18nPipe,
    OfflineStatusComponent,
  ],
  templateUrl: './admin-shell.component.html',
  styleUrl: './admin-shell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminShellComponent implements OnInit {
  readonly authService = inject(AuthService);
  readonly i18n = inject(I18nService);
  readonly realtime = inject(RealtimeService);
  private readonly router = inject(Router);

  readonly navItems: NavItem[] = [
    { path: '/admin/dashboard', labelKey: 'NAV.DASHBOARD', icon: '📊' },
    { path: '/admin/dispatch', labelKey: 'NAV.DISPATCH', icon: '🚚' },
    { path: '/admin/drivers', labelKey: 'NAV.DRIVERS', icon: '👥' },
    { path: '/admin/vehicles', labelKey: 'NAV.VEHICLES', icon: '🚛' },
    { path: '/admin/areas', labelKey: 'NAV.AREAS', icon: '🗺️' },
    { path: '/admin/timesheets', labelKey: 'NAV.TIMESHEETS', icon: '⏱️' },
    { path: '/admin/payroll', labelKey: 'NAV.PAYROLL', icon: '💰' },
    { path: '/admin/incidents', labelKey: 'NAV.INCIDENTS', icon: '🚨' },
    { path: '/admin/fines', labelKey: 'NAV.FINES', icon: '📄' },
    { path: '/admin/news', labelKey: 'NAV.NEWS', icon: '📢' },
    { path: '/admin/notifications', labelKey: 'NAV.NOTIFICATIONS', icon: '🔔' },
    { path: '/admin/audit', labelKey: 'NAV.AUDIT', icon: '📋' },
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
