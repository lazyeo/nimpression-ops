import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FinesService } from './services/fines.service';
import {
  FineDto,
  FineDetailDto,
  FineStatus,
  SubmitFineRequest,
  AcceptFineRequest,
  DisputeFineRequest,
  WaiveFineRequest,
} from './models/fines.models';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../../core/i18n/locale-date.pipe';
import { LocaleCurrencyPipe } from '../../../core/i18n/locale-currency.pipe';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { VehicleDto } from '../../../core/api/models/api-models';

@Component({
  selector: 'nim-admin-fines',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    I18nPipe,
    LocaleDatePipe,
    LocaleCurrencyPipe,
    IconComponent,
    StatusBadgeComponent,
  ],
  templateUrl: './fines.component.html',
  styleUrl: './fines.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FinesComponent implements OnInit {
  private readonly finesService = inject(FinesService);

  readonly isLoading = signal<boolean>(false);
  readonly isDetailLoading = signal<boolean>(false);
  readonly hasError = signal<boolean>(false);
  readonly isForbidden = signal<boolean>(false);
  readonly errorMessage = signal<string>('');

  readonly fines = signal<FineDto[]>([]);
  readonly totalRecords = signal<number>(0);
  readonly drivers = signal<any[]>([]);
  readonly vehicles = signal<VehicleDto[]>([]);

  // Filter signals
  readonly selectedDriverId = signal<string>('');
  readonly selectedVehicleId = signal<string>('');
  readonly selectedStatus = signal<string>('');
  readonly fromDate = signal<string>('');
  readonly toDate = signal<string>('');
  readonly searchTerm = signal<string>('');
  readonly currentPage = signal<number>(1);
  readonly pageSize = signal<number>(20);

  // Dialog states
  readonly isSubmitModalOpen = signal<boolean>(false);
  readonly isDetailModalOpen = signal<boolean>(false);
  readonly activeFineDetail = signal<FineDetailDto | null>(null);

  readonly isReviewModalOpen = signal<boolean>(false);
  readonly reviewMode = signal<'accept' | 'dispute' | 'waive'>('accept');
  readonly targetFine = signal<FineDto | null>(null);
  readonly reviewNote = signal<string>('');
  readonly reviewError = signal<string>('');
  readonly isReviewSubmitting = signal<boolean>(false);

  // Submit form state
  newDriverId = '';
  newVehicleId = '';
  newIssuedOn = '';
  newAuthority = '';
  newReference = '';
  newAmount: number | null = null;
  newReason = '';
  newPhotoKey = '';
  submitError = signal<string>('');
  isSubmittingFine = signal<boolean>(false);

  readonly totalPages = computed(() => {
    return Math.max(1, Math.ceil(this.totalRecords() / this.pageSize()));
  });

  ngOnInit(): void {
    this.loadDriversAndVehicles();
    this.loadFines();
  }

  loadDriversAndVehicles(): void {
    this.finesService.getDrivers().subscribe({
      next: (res) => this.drivers.set(res.items || []),
      error: () => {},
    });
    this.finesService.getVehicles().subscribe({
      next: (res) => this.vehicles.set(res.items || []),
      error: () => {},
    });
  }

  loadFines(): void {
    this.isLoading.set(true);
    this.hasError.set(false);
    this.isForbidden.set(false);
    this.errorMessage.set('');

    this.finesService
      .getFines({
        driverId: this.selectedDriverId() || undefined,
        vehicleId: this.selectedVehicleId() || undefined,
        status: (this.selectedStatus() as FineStatus) || undefined,
        fromDate: this.fromDate() || undefined,
        toDate: this.toDate() || undefined,
        searchTerm: this.searchTerm().trim() || undefined,
        page: this.currentPage(),
        pageSize: this.pageSize(),
      })
      .subscribe({
        next: (res) => {
          this.fines.set(res.items || []);
          this.totalRecords.set(res.totalCount || 0);
          this.isLoading.set(false);
        },
        error: (err) => {
          this.isLoading.set(false);
          if (err.status === 403) {
            this.isForbidden.set(true);
          } else {
            this.hasError.set(true);
            this.errorMessage.set(err.message || 'Failed to load traffic fines.');
          }
        },
      });
  }

  applyFilters(): void {
    this.currentPage.set(1);
    this.loadFines();
  }

  resetFilters(): void {
    this.selectedDriverId.set('');
    this.selectedVehicleId.set('');
    this.selectedStatus.set('');
    this.fromDate.set('');
    this.toDate.set('');
    this.searchTerm.set('');
    this.currentPage.set(1);
    this.loadFines();
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages() && page !== this.currentPage()) {
      this.currentPage.set(page);
      this.loadFines();
    }
  }

  // Detail modal with signed photo URL
  viewDetails(fine: FineDto): void {
    this.isDetailLoading.set(true);
    this.isDetailModalOpen.set(true);

    this.finesService.getFineById(fine.id).subscribe({
      next: (detail) => {
        this.activeFineDetail.set(detail);
        this.isDetailLoading.set(false);
      },
      error: () => {
        // Fallback with basic dto
        this.activeFineDetail.set({
          ...fine,
          ticketPhotoUrl: null,
          reviewedByUserId: null,
          reviewerName: null,
        });
        this.isDetailLoading.set(false);
      },
    });
  }

  closeDetailModal(): void {
    this.isDetailModalOpen.set(false);
    this.activeFineDetail.set(null);
  }

  // Submit Fine Modal
  openSubmitModal(): void {
    this.newDriverId = '';
    this.newVehicleId = '';
    this.newIssuedOn = new Date().toISOString().split('T')[0];
    this.newAuthority = '';
    this.newReference = '';
    this.newAmount = null;
    this.newReason = '';
    this.newPhotoKey = '';
    this.submitError.set('');
    this.isSubmitModalOpen.set(true);
  }

  closeSubmitModal(): void {
    this.isSubmitModalOpen.set(false);
  }

  submitFine(): void {
    if (
      !this.newVehicleId ||
      !this.newIssuedOn ||
      !this.newAuthority ||
      !this.newReference ||
      !this.newAmount ||
      !this.newReason
    ) {
      this.submitError.set('Please fill in all mandatory fields.');
      return;
    }

    this.isSubmittingFine.set(true);
    this.submitError.set('');

    const request: SubmitFineRequest = {
      driverId: this.newDriverId || null,
      vehicleId: this.newVehicleId,
      issuedOn: this.newIssuedOn,
      authority: this.newAuthority.trim(),
      reference: this.newReference.trim(),
      amount: Number(this.newAmount),
      currency: 'NZD',
      reason: this.newReason.trim(),
      ticketPhotoKey: this.newPhotoKey.trim() || null,
    };

    this.finesService.submitFine(request).subscribe({
      next: () => {
        this.isSubmittingFine.set(false);
        this.closeSubmitModal();
        this.loadFines();
      },
      error: (err) => {
        this.isSubmittingFine.set(false);
        this.submitError.set(err.error?.message || err.message || 'Failed to submit fine.');
      },
    });
  }

  // Review Lifecycle Transitions
  startReview(fine: FineDto): void {
    this.finesService.startReview(fine.id).subscribe({
      next: () => {
        this.loadFines();
      },
      error: (err) => {
        alert(err.error?.message || 'Failed to start review.');
      },
    });
  }

  openReviewModal(fine: FineDto, mode: 'accept' | 'dispute' | 'waive'): void {
    this.targetFine.set(fine);
    this.reviewMode.set(mode);
    this.reviewNote.set('');
    this.reviewError.set('');
    this.isReviewModalOpen.set(true);
  }

  closeReviewModal(): void {
    this.isReviewModalOpen.set(false);
    this.targetFine.set(null);
  }

  submitReviewDecision(): void {
    const fine = this.targetFine();
    if (!fine) return;

    const mode = this.reviewMode();
    const note = this.reviewNote().trim();

    if ((mode === 'dispute' || mode === 'waive') && !note) {
      this.reviewError.set('Review note / explanation is mandatory.');
      return;
    }

    this.isReviewSubmitting.set(true);
    this.reviewError.set('');

    let action$;
    if (mode === 'accept') {
      const req: AcceptFineRequest = { reviewNote: note || null };
      action$ = this.finesService.acceptFine(fine.id, req);
    } else if (mode === 'dispute') {
      const req: DisputeFineRequest = { reviewNote: note };
      action$ = this.finesService.disputeFine(fine.id, req);
    } else {
      const req: WaiveFineRequest = { reviewNote: note };
      action$ = this.finesService.waiveFine(fine.id, req);
    }

    action$.subscribe({
      next: () => {
        this.isReviewSubmitting.set(false);
        this.closeReviewModal();
        this.loadFines();
      },
      error: (err) => {
        this.isReviewSubmitting.set(false);
        this.reviewError.set(err.error?.message || err.message || 'Failed to submit review.');
      },
    });
  }
}
