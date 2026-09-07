import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  HostListener,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { I18nPipe } from '../../core/i18n/i18n.pipe';
import { RealtimeService } from '../../core/realtime/realtime.service';
import { OfflineStatusComponent } from '../../core/offline/offline-status.component';
import { IconComponent } from '../../shared/components/icon/icon.component';
import { LanguageSwitchComponent } from '../../shared/components/language-switch/language-switch.component';
import { toScreamingSnake } from '../../core/utils/case.utils';
import { UserRole } from '../../core/models/auth.models';

interface NavItem {
  path: string;
  labelKey: string;
  icon: string;
  roles?: UserRole[];
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
    IconComponent,
    LanguageSwitchComponent,
  ],
  templateUrl: './admin-shell.component.html',
  styleUrl: './admin-shell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminShellComponent implements OnInit {
  readonly toScreamingSnake = toScreamingSnake;
  readonly authService = inject(AuthService);
  readonly i18n = inject(I18nService);
  readonly realtime = inject(RealtimeService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly sidebarOpen = signal<boolean>(false);

  private readonly allNavItems: NavItem[] = [
    { path: '/admin/dashboard', labelKey: 'NAV.DASHBOARD', icon: 'dashboard' },
    { path: '/admin/dispatch', labelKey: 'NAV.DISPATCH', icon: 'dispatch' },
    { path: '/admin/drivers', labelKey: 'NAV.DRIVERS', icon: 'drivers' },
    { path: '/admin/vehicles', labelKey: 'NAV.VEHICLES', icon: 'vehicles' },
    { path: '/admin/areas', labelKey: 'NAV.AREAS', icon: 'areas' },
    { path: '/admin/timesheets', labelKey: 'NAV.TIMESHEETS', icon: 'timesheets' },
    { path: '/admin/payroll', labelKey: 'NAV.PAYROLL', icon: 'payroll', roles: ['Admin'] },
    { path: '/admin/incidents', labelKey: 'NAV.INCIDENTS', icon: 'incidents' },
    { path: '/admin/fines', labelKey: 'NAV.FINES', icon: 'fines' },
    { path: '/admin/news', labelKey: 'NAV.NEWS', icon: 'news' },
    { path: '/admin/notifications', labelKey: 'NAV.NOTIFICATIONS', icon: 'notifications', roles: ['Admin'] },
    { path: '/admin/audit', labelKey: 'NAV.AUDIT', icon: 'audit' },
  ];

  get navItems(): NavItem[] {
    const role = this.authService.userRole();
    return this.allNavItems.filter(
      (item) => !item.roles || (role !== null && item.roles.includes(role)),
    );
  }

  constructor() {
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => {
        this.closeSidebar();
      });
  }

  ngOnInit(): void {
    void this.realtime.startConnection();
  }

  @HostListener('window:keydown.escape')
  onEscape(): void {
    if (this.sidebarOpen()) {
      this.closeSidebar();
    }
  }

  toggleSidebar(): void {
    this.sidebarOpen.update((v) => !v);
  }

  openSidebar(): void {
    this.sidebarOpen.set(true);
  }

  closeSidebar(): void {
    this.sidebarOpen.set(false);
  }

  logout(): void {
    void this.realtime.stopConnection();
    this.authService.logout().subscribe();
  }
}
