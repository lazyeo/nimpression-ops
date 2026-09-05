import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { LocaleNumberPipe } from '../../../core/i18n/locale-number.pipe';
import { RealtimeService } from '../../../core/realtime/realtime.service';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';

export interface DashboardMetricsDto {
  activeDispatches: number;
  onlineDrivers: number;
  pendingIncidents: number;
  unresolvedFines: number;
}

@Component({
  selector: 'nim-admin-dashboard',
  standalone: true,
  imports: [CommonModule, I18nPipe, LocaleNumberPipe, IconComponent, StatusBadgeComponent],
  templateUrl: './admin-dashboard.component.html',
  styleUrl: './admin-dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminDashboardComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly realtime = inject(RealtimeService);

  readonly metrics = signal<DashboardMetricsDto>({
    activeDispatches: 12,
    onlineDrivers: 10,
    pendingIncidents: 0,
    unresolvedFines: 1,
  });

  ngOnInit(): void {
    this.loadMetrics();

    // Listen to realtime invalidation events to auto refresh
    this.realtime.invalidation$.subscribe(() => {
      this.loadMetrics();
    });
  }

  loadMetrics(): void {
    this.http.get<DashboardMetricsDto>('/api/dispatch/metrics').subscribe({
      next: (data) => this.metrics.set(data),
      error: () => {
        // Fallback default
      },
    });
  }
}
