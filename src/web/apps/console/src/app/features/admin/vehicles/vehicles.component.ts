import { BusinessLabelPipe } from '../../../core/i18n/business-label.pipe';
import { UserFacingErrorService } from '../../../core/errors/user-facing-error.service';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../../core/i18n/locale-date.pipe';
import { AuthService } from '../../../core/auth/auth.service';
import { RealtimeService } from '../../../core/realtime/realtime.service';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { DriverLookupOption, VehiclesService } from './services/vehicles.service';
import { toDateTimeLocalValue, parseDateTimeLocalToIso } from '../../../core/utils/date-utils';
import { toScreamingSnake } from '../../../core/utils/case.utils';
import {
  OdometerReadingDto,
  VehicleDetailDto,
  VehicleFilter,
  VehicleStatus,
  VehicleSummaryDto,
} from './models/vehicles.models';

export type ViewState = 'loading' | 'success' | 'empty' | 'error' | 'forbidden';

@Component({
  selector: 'nim-vehicles',
  standalone: true,
  imports: [
    BusinessLabelPipe,
    CommonModule,
    FormsModule,
    I18nPipe,
    LocaleDatePipe,
    IconComponent,
    StatusBadgeComponent,
  ],
  templateUrl: './vehicles.component.html',
  styleUrl: './vehicles.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VehiclesComponent implements OnInit {
  private readonly userFacingErrors = inject(UserFacingErrorService);
  private readonly vehiclesService = inject(VehiclesService);
  private readonly realtime = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);
  readonly auth = inject(AuthService);

  // States
  readonly state = signal<ViewState>('loading');
  readonly errorMessage = signal<string>('');
  readonly isSubmitting = signal<boolean>(false);
  readonly formError = signal<string | null>(null);

  // Data
  readonly vehicles = signal<VehicleSummaryDto[]>([]);
  readonly drivers = signal<DriverLookupOption[]>([]);
  readonly odometerReadings = signal<OdometerReadingDto[]>([]);

  // Computed
  readonly serviceDueCount = computed(() => {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const thirtyDaysAhead = new Date(today);
    thirtyDaysAhead.setDate(thirtyDaysAhead.getDate() + 30);

    return this.vehicles().filter((v) => {
      if (v.isServiceDue) {
        return true;
      }
      return (
        this.isDateExpiredOrExpiring(v.wofExpiry, thirtyDaysAhead) ||
        this.isDateExpiredOrExpiring(v.cofExpiry, thirtyDaysAhead) ||
        this.isDateExpiredOrExpiring(v.insuranceExpiry, thirtyDaysAhead)
      );
    }).length;
  });

  // Filters & Pagination
  readonly searchTerm = signal<string>('');
  readonly selectedStatus = signal<string>('');
  readonly serviceDueOnly = signal<boolean>(false);
  readonly currentPage = signal<number>(1);
  readonly pageSize = signal<number>(20);
  readonly totalCount = signal<number>(0);
  readonly totalPages = signal<number>(1);

  // Modals
  readonly isCreateModalOpen = signal<boolean>(false);
  readonly isEditModalOpen = signal<boolean>(false);
  readonly isAssignModalOpen = signal<boolean>(false);
  readonly isServiceModalOpen = signal<boolean>(false);
  readonly isOdometerModalOpen = signal<boolean>(false);
  readonly isDetailsModalOpen = signal<boolean>(false);

  readonly selectedVehicle = signal<VehicleSummaryDto | null>(null);
  readonly selectedVehicleDetail = signal<VehicleDetailDto | null>(null);

  // Form States
  createForm = {
    rego: '',
    make: '',
    model: '',
    year: 2024,
    vinEnc: '',
    odometerKm: 0,
    serviceIntervalKm: 10000,
    lastServiceOdometerKm: 0,
    wofExpiry: '',
    cofExpiry: '',
    insuranceExpiry: '',
    status: 'Active' as VehicleStatus,
  };

  editForm = {
    wofExpiry: '',
    cofExpiry: '',
    insuranceExpiry: '',
    status: 'Active' as VehicleStatus,
  };

  assignForm = {
    driverId: '',
    assignedAt: '',
  };

  serviceForm = {
    serviceOdometerKm: 0,
  };

  odometerForm = {
    driverId: '',
    readingKm: 0,
  };

  ngOnInit(): void {
    if (!this.auth.isAdminOrDispatcher()) {
      this.state.set('forbidden');
      return;
    }

    this.loadDrivers();
    this.loadVehicles();

    // SignalR Realtime Invalidation Subscription
    this.realtime.invalidation$
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        filter((msg) => msg.kind.startsWith('vehicle.')),
      )
      .subscribe(() => {
        this.loadVehicles(false);
      });
  }

  isAdmin(): boolean {
    return this.auth.isAdmin();
  }

  loadVehicles(setLoading = true): void {
    if (setLoading) {
      this.state.set('loading');
    }
    this.errorMessage.set('');

    const filter: VehicleFilter = {
      search: this.searchTerm() || undefined,
      status: (this.selectedStatus() as VehicleStatus) || undefined,
      serviceDueOnly: this.serviceDueOnly() ? true : undefined,
      page: this.currentPage(),
      pageSize: this.pageSize(),
    };

    this.vehiclesService.getVehicles(filter).subscribe({
      next: (res) => {
        this.vehicles.set(res.items || []);
        this.totalCount.set(res.totalCount || 0);
        this.totalPages.set(res.totalPages || 1);

        if (!res.items || res.items.length === 0) {
          this.state.set('empty');
        } else {
          this.state.set('success');
        }
      },
      error: (err: HttpErrorResponse) => {
        if (err.status === 403) {
          this.state.set('forbidden');
        } else {
          this.state.set('error');
          this.errorMessage.set(this.userFacingErrors.format(err));
        }
      },
    });
  }

  loadDrivers(): void {
    this.vehiclesService.getDrivers().subscribe({
      next: (res) => this.drivers.set(res.items || []),
      error: () => {},
    });
  }

  onSearchInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.searchTerm.set(input.value);
    this.currentPage.set(1);
    this.loadVehicles();
  }

  onStatusChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.selectedStatus.set(select.value);
    this.currentPage.set(1);
    this.loadVehicles();
  }

  onServiceDueToggle(event: Event): void {
    const checkbox = event.target as HTMLInputElement;
    this.serviceDueOnly.set(checkbox.checked);
    this.currentPage.set(1);
    this.loadVehicles();
  }

  clearFilters(): void {
    this.searchTerm.set('');
    this.selectedStatus.set('');
    this.serviceDueOnly.set(false);
    this.currentPage.set(1);
    this.loadVehicles();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage.set(page);
    this.loadVehicles();
  }

  readonly toScreamingSnake = toScreamingSnake;

  // --- Modals ---

  openCreateModal(): void {
    this.createForm = {
      rego: '',
      make: '',
      model: '',
      year: new Date().getFullYear(),
      vinEnc: '',
      odometerKm: 0,
      serviceIntervalKm: 10000,
      lastServiceOdometerKm: 0,
      wofExpiry: '',
      cofExpiry: '',
      insuranceExpiry: '',
      status: 'Active',
    };
    this.formError.set(null);
    this.isCreateModalOpen.set(true);
  }

  closeCreateModal(): void {
    this.isCreateModalOpen.set(false);
  }

  submitCreateVehicle(): void {
    if (
      !this.createForm.rego ||
      !this.createForm.make ||
      !this.createForm.model ||
      !this.createForm.vinEnc
    ) {
      return;
    }
    this.isSubmitting.set(true);
    this.formError.set(null);

    this.vehiclesService
      .createVehicle({
        rego: this.createForm.rego.trim().toUpperCase(),
        make: this.createForm.make.trim(),
        model: this.createForm.model.trim(),
        year: this.createForm.year,
        vinEnc: this.createForm.vinEnc.trim(),
        odometerKm: this.createForm.odometerKm,
        serviceIntervalKm: this.createForm.serviceIntervalKm,
        lastServiceOdometerKm: this.createForm.lastServiceOdometerKm || undefined,
        wofExpiry: this.createForm.wofExpiry || undefined,
        cofExpiry: this.createForm.cofExpiry || undefined,
        insuranceExpiry: this.createForm.insuranceExpiry || undefined,
        status: this.createForm.status,
      })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.closeCreateModal();
          this.loadVehicles();
        },
        error: (err: HttpErrorResponse) => {
          this.isSubmitting.set(false);
          if (err.status === 409) {
            this.formError.set(this.userFacingErrors.format(err));
          } else {
            this.formError.set(this.userFacingErrors.format(err));
          }
        },
      });
  }

  openEditModal(veh: VehicleSummaryDto): void {
    this.selectedVehicle.set(veh);
    this.formError.set(null);
    this.editForm = {
      wofExpiry: veh.wofExpiry || '',
      cofExpiry: veh.cofExpiry || '',
      insuranceExpiry: veh.insuranceExpiry || '',
      status: veh.status,
    };
    this.isEditModalOpen.set(true);
  }

  closeEditModal(): void {
    this.isEditModalOpen.set(false);
    this.selectedVehicle.set(null);
  }

  submitEditVehicle(): void {
    const veh = this.selectedVehicle();
    if (!veh) return;

    this.isSubmitting.set(true);
    this.formError.set(null);

    this.vehiclesService
      .updateVehicle(veh.id, {
        wofExpiry: this.editForm.wofExpiry || undefined,
        cofExpiry: this.editForm.cofExpiry || undefined,
        insuranceExpiry: this.editForm.insuranceExpiry || undefined,
        status: this.editForm.status,
      })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.closeEditModal();
          this.loadVehicles();
        },
        error: (err: HttpErrorResponse) => {
          this.isSubmitting.set(false);
          this.formError.set(this.userFacingErrors.format(err));
        },
      });
  }

  openAssignModal(veh: VehicleSummaryDto): void {
    this.selectedVehicle.set(veh);
    this.assignForm = {
      driverId: this.drivers().length > 0 ? this.drivers()[0].id : '',
      assignedAt: toDateTimeLocalValue(new Date()),
    };
    this.formError.set(null);
    this.isAssignModalOpen.set(true);
  }

  closeAssignModal(): void {
    this.isAssignModalOpen.set(false);
    this.selectedVehicle.set(null);
  }

  submitAssignVehicle(): void {
    const veh = this.selectedVehicle();
    if (!veh || !this.assignForm.driverId) return;

    this.isSubmitting.set(true);
    this.formError.set(null);

    this.vehiclesService
      .assignVehicle(veh.id, {
        driverId: this.assignForm.driverId,
        assignedAt: this.assignForm.assignedAt
          ? parseDateTimeLocalToIso(this.assignForm.assignedAt)
          : undefined,
      })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.closeAssignModal();
          this.loadVehicles();
        },
        error: (err: HttpErrorResponse) => {
          this.isSubmitting.set(false);
          if (err.status === 409) {
            this.formError.set(this.userFacingErrors.format(err));
          } else {
            this.formError.set(this.userFacingErrors.format(err));
          }
        },
      });
  }

  releaseVehicle(veh: VehicleSummaryDto): void {
    if (!confirm(`Release vehicle assignment for ${veh.currentDriverName || 'driver'}?`)) return;

    this.vehiclesService.getActiveAssignment(veh.id).subscribe({
      next: (asg) => {
        if (!asg) {
          this.loadVehicles();
          return;
        }
        this.vehiclesService.releaseAssignment(asg.id).subscribe({
          next: () => this.loadVehicles(),
          error: (err: HttpErrorResponse) => {
            alert(this.userFacingErrors.format(err));
          },
        });
      },
      error: () => this.loadVehicles(),
    });
  }

  openServiceModal(veh: VehicleSummaryDto): void {
    this.selectedVehicle.set(veh);
    this.serviceForm = {
      serviceOdometerKm: veh.odometerKm,
    };
    this.formError.set(null);
    this.isServiceModalOpen.set(true);
  }

  closeServiceModal(): void {
    this.isServiceModalOpen.set(false);
    this.selectedVehicle.set(null);
  }

  submitRecordService(): void {
    const veh = this.selectedVehicle();
    if (!veh || !this.serviceForm.serviceOdometerKm) return;

    this.isSubmitting.set(true);
    this.formError.set(null);

    this.vehiclesService.recordService(veh.id, this.serviceForm.serviceOdometerKm).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.closeServiceModal();
        this.loadVehicles();
      },
      error: (err: HttpErrorResponse) => {
        this.isSubmitting.set(false);
        this.formError.set(this.userFacingErrors.format(err));
      },
    });
  }

  openOdometerModal(veh: VehicleSummaryDto): void {
    this.selectedVehicle.set(veh);
    this.odometerForm = {
      driverId: veh.currentDriverId || (this.drivers().length > 0 ? this.drivers()[0].id : ''),
      readingKm: veh.odometerKm,
    };
    this.formError.set(null);
    this.isOdometerModalOpen.set(true);
  }

  closeOdometerModal(): void {
    this.isOdometerModalOpen.set(false);
    this.selectedVehicle.set(null);
  }

  submitRecordOdometer(): void {
    const veh = this.selectedVehicle();
    if (!veh || !this.odometerForm.driverId || !this.odometerForm.readingKm) return;

    this.isSubmitting.set(true);
    this.formError.set(null);

    this.vehiclesService
      .recordOdometerReading(veh.id, {
        driverId: this.odometerForm.driverId,
        readingKm: this.odometerForm.readingKm,
        recordedAt: new Date().toISOString(),
        source: 'AdminConsole',
      })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.closeOdometerModal();
          this.loadVehicles();
        },
        error: (err: HttpErrorResponse) => {
          this.isSubmitting.set(false);
          this.formError.set(this.userFacingErrors.format(err));
        },
      });
  }

  openDetailsModal(veh: VehicleSummaryDto): void {
    this.vehiclesService.getVehicleById(veh.id).subscribe({
      next: (detail) => {
        this.selectedVehicleDetail.set(detail);
        this.isDetailsModalOpen.set(true);
      },
      error: () => alert('Failed to load vehicle details'),
    });

    this.vehiclesService.getOdometerReadings(veh.id, 20).subscribe({
      next: (readings) => this.odometerReadings.set(readings || []),
      error: () => this.odometerReadings.set([]),
    });
  }

  closeDetailsModal(): void {
    this.isDetailsModalOpen.set(false);
    this.selectedVehicleDetail.set(null);
    this.odometerReadings.set([]);
  }

  private isDateExpiredOrExpiring(dateStr?: string | null, thresholdDate?: Date): boolean {
    if (!dateStr) return false;
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) return false;
    const threshold =
      thresholdDate ??
      (() => {
        const t = new Date();
        t.setHours(0, 0, 0, 0);
        t.setDate(t.getDate() + 30);
        return t;
      })();
    return d <= threshold;
  }
}
