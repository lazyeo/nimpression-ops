import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  VehicleStatus,
  JobTaskStatus,
  TaskPriority,
  ShiftStatus,
  FineStatus,
  PayPeriodStatus,
  PayBasis,
  DriverStatus,
  UserRole,
  UserStatus,
  IncidentSeverity,
  DataSubjectRequestKind,
  NewsAudience,
  PartnerKind,
} from '../../../core/api/models/api-models';
import { IconComponent } from '../icon/icon.component';

export type DomainStatus =
  | VehicleStatus
  | JobTaskStatus
  | TaskPriority
  | ShiftStatus
  | FineStatus
  | PayPeriodStatus
  | PayBasis
  | DriverStatus
  | UserRole
  | UserStatus
  | IncidentSeverity
  | DataSubjectRequestKind
  | NewsAudience
  | PartnerKind
  | string;

export type BadgeVariant =
  | 'success'
  | 'warning'
  | 'danger'
  | 'info'
  | 'neutral'
  | 'purple'
  | 'orange';

export type BadgeSize = 'sm' | 'md' | 'lg';

const VARIANT_LOOKUP: Record<string, BadgeVariant> = {
  // Success (Green)
  Active: 'success',
  Completed: 'success',
  Accepted: 'success',
  Finalised: 'success',
  Paid: 'success',
  All: 'success',
  Online: 'success',
  Synced: 'success',
  Sent: 'success',
  Passed: 'success',

  // Warning (Amber / Yellow)
  Maintenance: 'warning',
  Assigned: 'warning',
  Acknowledged: 'warning',
  UnderReview: 'warning',
  Calculating: 'warning',
  Suspended: 'warning',
  OnLeave: 'warning',
  Moderate: 'warning',
  High: 'warning',
  Urgent: 'warning',
  Syncing: 'warning',
  Inspection: 'warning',
  ExpiringSoon: 'warning',

  // Danger (Red)
  Inactive: 'danger',
  Cancelled: 'danger',
  Disputed: 'danger',
  Terminated: 'danger',
  Critical: 'danger',
  Major: 'danger',
  Offline: 'danger',
  Failed: 'danger',
  Expired: 'danger',

  // Info (Sky / Blue)
  InProgress: 'info',
  Submitted: 'info',
  Medium: 'info',
  Hourly: 'info',
  Trip: 'info',
  Dispatcher: 'info',
  Drivers: 'info',
  Export: 'info',
  Deletion: 'info',
  Insurer: 'info',
  Notified: 'info',

  // Neutral (Slate / Gray)
  Draft: 'neutral',
  Decommissioned: 'neutral',
  AutoClosed: 'neutral',
  Waived: 'neutral',
  Open: 'neutral',
  Low: 'neutral',
  Minor: 'neutral',
  Rectification: 'neutral',
  Admin: 'neutral',
  Standard: 'neutral',
  Driver: 'info',
};

@Component({
  selector: 'nim-status-badge',
  standalone: true,
  imports: [CommonModule, IconComponent],
  templateUrl: './status-badge.component.html',
  styleUrls: ['./status-badge.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusBadgeComponent {
  readonly status = input.required<DomainStatus>();
  readonly variant = input<BadgeVariant | undefined>(undefined);
  readonly size = input<BadgeSize>('md');
  readonly label = input<string | undefined>(undefined);
  readonly icon = input<string | undefined>(undefined);

  readonly resolvedVariant = computed<BadgeVariant>(() => {
    const explicit = this.variant();
    if (explicit) return explicit;
    const s = this.status();
    if (!s) return 'neutral';
    return VARIANT_LOOKUP[s] ?? 'neutral';
  });

  readonly displayLabel = computed<string>(() => {
    const custom = this.label();
    if (custom !== undefined) return custom;
    return this.status() || '';
  });

  readonly iconSize = computed<number>(() => {
    const sz = this.size();
    if (sz === 'sm') return 10;
    if (sz === 'lg') return 16;
    return 12;
  });
}
