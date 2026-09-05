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

export type BadgeDomainStatus =
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
  | PartnerKind;

export type ExtraBadgeStatus =
  | 'Overdue'
  | 'Expired'
  | 'ExpiringSoon'
  | 'ServiceDue'
  | 'ServiceOk'
  | 'InsurerNotified'
  | 'InsurerStandard'
  | 'TopUpApplied'
  | 'TopUpNone'
  | 'HourlyApplied'
  | 'TripApplied'
  | 'ZeroDeduction'
  | 'Online'
  | 'Offline'
  | 'Synced'
  | 'Syncing'
  | 'Sent'
  | 'Failed'
  | 'Passed'
  | 'Standard'
  | 'NoGps'
  | 'HasGps';

export type BadgeStatus = BadgeDomainStatus | ExtraBadgeStatus;

export type BadgeVariant =
  | 'success'
  | 'warning'
  | 'danger'
  | 'info'
  | 'neutral'
  | 'purple'
  | 'orange';

export type BadgeSize = 'sm' | 'md' | 'lg';

export const VARIANT_LOOKUP: Record<BadgeStatus, BadgeVariant> = {
  // VehicleStatus
  Active: 'success',
  Maintenance: 'warning',
  Inactive: 'danger',
  Decommissioned: 'neutral',

  // JobTaskStatus
  Draft: 'neutral',
  Assigned: 'warning',
  Acknowledged: 'warning',
  InProgress: 'info',
  Completed: 'success',
  Cancelled: 'danger',

  // TaskPriority
  Low: 'neutral',
  Medium: 'info',
  High: 'warning',
  Urgent: 'warning',

  // ShiftStatus (Active, Completed, Cancelled already mapped)
  AutoClosed: 'neutral',

  // FineStatus
  Submitted: 'info',
  UnderReview: 'warning',
  Accepted: 'success',
  Disputed: 'danger',
  Waived: 'neutral',

  // PayPeriodStatus
  Open: 'neutral',
  Calculating: 'warning',
  Finalised: 'success',
  Paid: 'success',

  // PayBasis
  Hourly: 'info',
  Trip: 'info',

  // DriverStatus (Active, Inactive already mapped)
  Suspended: 'warning',
  OnLeave: 'warning',
  Terminated: 'danger',

  // UserRole
  Admin: 'neutral',
  Dispatcher: 'info',
  Driver: 'info',

  // UserStatus (Active, Inactive, Suspended already mapped)

  // IncidentSeverity
  Minor: 'neutral',
  Moderate: 'warning',
  Major: 'danger',
  Critical: 'danger',

  // DataSubjectRequestKind
  Export: 'info',
  Deletion: 'info',
  Rectification: 'neutral',

  // NewsAudience (All, Drivers, Dispatchers)
  All: 'success',
  Drivers: 'info',
  Dispatchers: 'info',

  // PartnerKind (Insurer, Maintenance, Inspection)
  Insurer: 'info',
  Inspection: 'warning',

  // Extra UI / Template statuses
  Overdue: 'warning',
  Expired: 'danger',
  ExpiringSoon: 'warning',
  ServiceDue: 'danger',
  ServiceOk: 'success',
  InsurerNotified: 'success',
  InsurerStandard: 'neutral',
  TopUpApplied: 'warning',
  TopUpNone: 'neutral',
  HourlyApplied: 'success',
  TripApplied: 'success',
  ZeroDeduction: 'success',
  Online: 'success',
  Offline: 'danger',
  Synced: 'success',
  Syncing: 'warning',
  Sent: 'success',
  Failed: 'danger',
  Passed: 'success',
  Standard: 'neutral',
  NoGps: 'neutral',
  HasGps: 'success',
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
  readonly status = input<BadgeStatus | (string & {}) | ''>('');
  readonly variant = input<BadgeVariant | undefined>(undefined);
  readonly size = input<BadgeSize>('md');
  readonly label = input<string | undefined>(undefined);
  readonly icon = input<string | undefined>(undefined);
  readonly dot = input<boolean>(false);

  readonly resolvedVariant = computed<BadgeVariant>(() => {
    const explicit = this.variant();
    if (explicit) return explicit;
    const s = this.status();
    if (!s) return 'neutral';
    if (Object.prototype.hasOwnProperty.call(VARIANT_LOOKUP, s)) {
      return VARIANT_LOOKUP[s as BadgeStatus];
    }
    console.error(
      `[StatusBadgeComponent] Unrecognized status value: "${s}". Not mapped in VARIANT_LOOKUP. Rendering 'danger' alert per CLAUDE.md 2.3.`,
    );
    return 'danger';
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
