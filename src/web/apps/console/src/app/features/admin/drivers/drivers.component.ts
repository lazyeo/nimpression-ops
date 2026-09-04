import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { CommonModule, SlicePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../../core/i18n/locale-date.pipe';
import { AuthService } from '../../../core/auth/auth.service';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { AreaLookupOption, DriversService } from './services/drivers.service';
import {
  DriverDetailDto,
  DriverFilter,
  DriverLicenceAlertDto,
  DriverStatus,
  DriverSummaryDto,
} from './models/drivers.models';

export type ViewState = 'loading' | 'success' | 'empty' | 'error' | 'forbidden';

@Component({
  selector: 'nim-drivers',
  standalone: true,
  imports: [CommonModule, FormsModule, I18nPipe, LocaleDatePipe, SlicePipe, IconComponent, StatusBadgeComponent],
  templateUrl: './drivers.component.html',
  styleUrl: './drivers.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DriversComponent implements OnInit {
  private readonly driversService = inject(DriversService);
  readonly auth = inject(AuthService);

  // States
  readonly state = signal<ViewState>('loading');
  readonly errorMessage = signal<string>('');
  readonly isSubmitting = signal<boolean>(false);
  readonly formError = signal<string | null>(null);

  // Data
  readonly drivers = signal<DriverSummaryDto[]>([]);
  readonly alerts = signal<DriverLicenceAlertDto[]>([]);
  readonly areas = signal<AreaLookupOption[]>([]);

  // Filters & Pagination
  readonly searchTerm = signal<string>('');
  readonly selectedStatus = signal<string>('');
  readonly selectedAreaId = signal<string>('');
  readonly currentPage = signal<number>(1);
  readonly pageSize = signal<number>(20);
  readonly totalCount = signal<number>(0);
  readonly totalPages = signal<number>(1);

  // Modals
  readonly isCreateModalOpen = signal<boolean>(false);
  readonly isEditModalOpen = signal<boolean>(false);
  readonly isAvatarModalOpen = signal<boolean>(false);
  readonly isDeactivateModalOpen = signal<boolean>(false);
  readonly isDetailsModalOpen = signal<boolean>(false);

  readonly selectedDriver = signal<DriverSummaryDto | null>(null);
  readonly selectedDriverDetail = signal<DriverDetailDto | null>(null);

  // Avatar Upload State
  selectedAvatarFile: File | null = null;
  avatarPreviewUrl: string | null = null;

  // Deactivate State
  deactivateReason = '';

  // Create Form State
  createForm = {
    displayName: '',
    email: '',
    password: '',
    employeeNo: '',
    licenceClass: 'Class 2 Heavy Rigid',
    licenceExpiry: '',
    hiredOn: new Date().toISOString().slice(0, 10),
    phone: '',
    address: '',
    emergencyContact: '',
    hourlyRateAmount: 32.5,
    hourlyRateCurrency: 'NZD',
    perTripRateAmount: 15.0,
    perTripRateCurrency: 'NZD',
    perKmRateAmount: 0.75,
    perKmRateCurrency: 'NZD',
    areaIds: [] as string[],
  };

  // Edit Form State
  editForm = {
    displayName: '',
    licenceClass: '',
    licenceExpiry: '',
    hourlyRateAmount: 30.0,
    hourlyRateCurrency: 'NZD',
    perTripRateAmount: 15.0,
    perTripRateCurrency: 'NZD',
    perKmRateAmount: 0.75,
    perKmRateCurrency: 'NZD',
    phone: '',
    address: '',
    emergencyContact: '',
    status: 'Active' as DriverStatus,
  };

  ngOnInit(): void {
    if (!this.auth.isAdminOrDispatcher()) {
      this.state.set('forbidden');
      return;
    }

    this.loadAreas();
    this.loadDrivers();
    this.loadLicenceAlerts();
  }

  isAdmin(): boolean {
    return this.auth.isAdmin();
  }

  loadDrivers(setLoading = true): void {
    if (setLoading) {
      this.state.set('loading');
    }
    this.errorMessage.set('');

    const filter: DriverFilter = {
      searchTerm: this.searchTerm() || undefined,
      status: (this.selectedStatus() as DriverStatus) || undefined,
      areaId: this.selectedAreaId() || undefined,
      page: this.currentPage(),
      pageSize: this.pageSize(),
    };

    this.driversService.getDrivers(filter).subscribe({
      next: (res) => {
        this.drivers.set(res.items || []);
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
          this.errorMessage.set(err.error?.message || err.message || 'Error loading drivers');
        }
      },
    });
  }

  loadLicenceAlerts(): void {
    this.driversService.getLicenceAlerts(30).subscribe({
      next: (alerts) => this.alerts.set(alerts || []),
      error: () => this.alerts.set([]),
    });
  }

  loadAreas(): void {
    this.driversService.getAreas().subscribe({
      next: (res) => this.areas.set(res.items || []),
      error: () => {},
    });
  }

  onSearchInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.searchTerm.set(input.value);
    this.currentPage.set(1);
    this.loadDrivers();
  }

  onStatusChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.selectedStatus.set(select.value);
    this.currentPage.set(1);
    this.loadDrivers();
  }

  onAreaChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.selectedAreaId.set(select.value);
    this.currentPage.set(1);
    this.loadDrivers();
  }

  clearFilters(): void {
    this.searchTerm.set('');
    this.selectedStatus.set('');
    this.selectedAreaId.set('');
    this.currentPage.set(1);
    this.loadDrivers();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage.set(page);
    this.loadDrivers();
  }

  getStatusKey(status: string): string {
    switch (status) {
      case 'Active':
        return 'ACTIVE';
      case 'Inactive':
        return 'INACTIVE';
      case 'Suspended':
        return 'SUSPENDED';
      case 'OnLeave':
        return 'ON_LEAVE';
      case 'Terminated':
        return 'TERMINATED';
      default:
        return status.toUpperCase();
    }
  }

  // --- Modals ---

  openCreateModal(): void {
    this.createForm = {
      displayName: '',
      email: '',
      password: '',
      employeeNo: '',
      licenceClass: 'Class 2 Heavy Rigid',
      licenceExpiry: '',
      hiredOn: new Date().toISOString().slice(0, 10),
      phone: '',
      address: '',
      emergencyContact: '',
      hourlyRateAmount: 32.5,
      hourlyRateCurrency: 'NZD',
      perTripRateAmount: 15.0,
      perTripRateCurrency: 'NZD',
      perKmRateAmount: 0.75,
      perKmRateCurrency: 'NZD',
      areaIds: [],
    };
    this.formError.set(null);
    this.isCreateModalOpen.set(true);
  }

  closeCreateModal(): void {
    this.isCreateModalOpen.set(false);
  }

  isAreaSelectedInCreate(areaId: string): boolean {
    return this.createForm.areaIds.includes(areaId);
  }

  toggleAreaInCreate(areaId: string): void {
    const idx = this.createForm.areaIds.indexOf(areaId);
    if (idx >= 0) {
      this.createForm.areaIds.splice(idx, 1);
    } else {
      this.createForm.areaIds.push(areaId);
    }
  }

  submitCreateDriver(): void {
    if (
      !this.createForm.displayName ||
      !this.createForm.email ||
      !this.createForm.employeeNo ||
      !this.createForm.licenceExpiry
    ) {
      return;
    }
    this.isSubmitting.set(true);
    this.formError.set(null);

    this.driversService
      .createDriver({
        displayName: this.createForm.displayName,
        email: this.createForm.email,
        password: this.createForm.password || undefined,
        employeeNo: this.createForm.employeeNo,
        licenceClass: this.createForm.licenceClass,
        licenceExpiry: this.createForm.licenceExpiry,
        hiredOn: this.createForm.hiredOn,
        hourlyRateAmount: this.createForm.hourlyRateAmount,
        hourlyRateCurrency: 'NZD',
        perTripRateAmount: this.createForm.perTripRateAmount,
        perTripRateCurrency: 'NZD',
        perKmRateAmount: this.createForm.perKmRateAmount,
        perKmRateCurrency: 'NZD',
        phone: this.createForm.phone,
        address: this.createForm.address,
        emergencyContact: this.createForm.emergencyContact,
        areaIds: this.createForm.areaIds.length > 0 ? this.createForm.areaIds : undefined,
      })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.closeCreateModal();
          this.loadDrivers();
        },
        error: (err: HttpErrorResponse) => {
          this.isSubmitting.set(false);
          this.formError.set(err.error?.message || err.error?.detail || err.message || 'Failed to create driver');
        },
      });
  }

  openEditModal(driver: DriverSummaryDto): void {
    this.selectedDriver.set(driver);
    this.formError.set(null);

    this.driversService.getDriverById(driver.id).subscribe({
      next: (detail) => {
        this.selectedDriverDetail.set(detail);
        this.editForm = {
          displayName: detail.displayName,
          licenceClass: detail.licenceClass,
          licenceExpiry: detail.licenceExpiry,
          hourlyRateAmount: detail.hourlyRateAmount,
          hourlyRateCurrency: detail.hourlyRateCurrency || 'NZD',
          perTripRateAmount: detail.perTripRateAmount,
          perTripRateCurrency: detail.perTripRateCurrency || 'NZD',
          perKmRateAmount: detail.perKmRateAmount,
          perKmRateCurrency: detail.perKmRateCurrency || 'NZD',
          phone: detail.phone,
          address: detail.address,
          emergencyContact: detail.emergencyContact,
          status: detail.status,
        };
        this.isEditModalOpen.set(true);
      },
      error: (err: HttpErrorResponse) => {
        alert(err.error?.message || 'Failed to load driver details for editing');
      },
    });
  }

  closeEditModal(): void {
    this.isEditModalOpen.set(false);
    this.selectedDriver.set(null);
    this.selectedDriverDetail.set(null);
  }

  submitEditDriver(): void {
    const detail = this.selectedDriverDetail();
    if (!detail) return;

    this.isSubmitting.set(true);
    this.formError.set(null);

    this.driversService
      .updateDriver(detail.id, {
        displayName: this.editForm.displayName,
        licenceClass: this.editForm.licenceClass,
        licenceExpiry: this.editForm.licenceExpiry,
        hourlyRateAmount: this.editForm.hourlyRateAmount,
        hourlyRateCurrency: 'NZD',
        perTripRateAmount: this.editForm.perTripRateAmount,
        perTripRateCurrency: 'NZD',
        perKmRateAmount: this.editForm.perKmRateAmount,
        perKmRateCurrency: 'NZD',
        phone: this.editForm.phone,
        address: this.editForm.address,
        emergencyContact: this.editForm.emergencyContact,
        status: this.editForm.status,
      })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.closeEditModal();
          this.loadDrivers();
        },
        error: (err: HttpErrorResponse) => {
          this.isSubmitting.set(false);
          this.formError.set(err.error?.message || err.error?.detail || err.message || 'Failed to update driver');
        },
      });
  }

  openAvatarModal(driver: DriverSummaryDto): void {
    this.selectedDriver.set(driver);
    this.selectedAvatarFile = null;
    this.avatarPreviewUrl = null;
    this.formError.set(null);
    this.isAvatarModalOpen.set(true);
  }

  closeAvatarModal(): void {
    this.isAvatarModalOpen.set(false);
    this.selectedDriver.set(null);
    this.selectedAvatarFile = null;
    this.avatarPreviewUrl = null;
  }

  onAvatarFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;

    const file = input.files[0];
    if (file.size > 2 * 1024 * 1024) {
      this.formError.set('File size exceeds 2MB limit.');
      this.selectedAvatarFile = null;
      return;
    }

    if (!['image/jpeg', 'image/png'].includes(file.type)) {
      this.formError.set('Unsupported file format. Please upload JPG or PNG.');
      this.selectedAvatarFile = null;
      return;
    }

    this.selectedAvatarFile = file;
    this.formError.set(null);

    const reader = new FileReader();
    reader.onload = () => {
      this.avatarPreviewUrl = reader.result as string;
    };
    reader.readAsDataURL(file);
  }

  submitAvatarUpload(): void {
    const driver = this.selectedDriver();
    if (!driver || !this.selectedAvatarFile) return;

    this.isSubmitting.set(true);
    this.formError.set(null);

    this.driversService.uploadAvatar(driver.id, this.selectedAvatarFile).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.closeAvatarModal();
        this.loadDrivers();
      },
      error: (err: HttpErrorResponse) => {
        this.isSubmitting.set(false);
        if (err.status === 415) {
          this.formError.set('Invalid image format detected by magic byte scanner (415).');
        } else {
          this.formError.set(err.error?.message || err.error?.detail || 'Failed to upload avatar');
        }
      },
    });
  }

  openDeactivateModal(driver: DriverSummaryDto): void {
    this.selectedDriver.set(driver);
    this.deactivateReason = '';
    this.formError.set(null);
    this.isDeactivateModalOpen.set(true);
  }

  closeDeactivateModal(): void {
    this.isDeactivateModalOpen.set(false);
    this.selectedDriver.set(null);
  }

  submitDeactivate(): void {
    const driver = this.selectedDriver();
    if (!driver) return;

    this.isSubmitting.set(true);
    this.formError.set(null);

    this.driversService
      .deactivateDriver(driver.id, { reason: this.deactivateReason || undefined })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.closeDeactivateModal();
          this.loadDrivers();
        },
        error: (err: HttpErrorResponse) => {
          this.isSubmitting.set(false);
          this.formError.set(err.error?.message || err.error?.detail || 'Failed to deactivate driver');
        },
      });
  }

  openDetailsModal(driver: DriverSummaryDto): void {
    this.driversService.getDriverById(driver.id).subscribe({
      next: (detail) => {
        this.selectedDriverDetail.set(detail);
        this.isDetailsModalOpen.set(true);
      },
      error: (err: HttpErrorResponse) => {
        alert(err.error?.message || 'Failed to load driver profile');
      },
    });
  }

  closeDetailsModal(): void {
    this.isDetailsModalOpen.set(false);
    this.selectedDriverDetail.set(null);
  }
}
