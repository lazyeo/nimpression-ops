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
import { IncidentsService } from './services/incidents.service';
import {
  IncidentReportDto,
  IncidentReportDetailDto,
  IncidentSeverity,
  ReportIncidentRequest,
} from './models/incidents.models';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../../core/i18n/locale-date.pipe';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { VehicleDto } from '../../../core/api/models/api-models';

@Component({
  selector: 'nim-admin-incidents',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    I18nPipe,
    LocaleDatePipe,
    IconComponent,
  ],
  templateUrl: './incidents.component.html',
  styleUrl: './incidents.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IncidentsComponent implements OnInit {
  private readonly incidentsService = inject(IncidentsService);

  readonly isLoading = signal<boolean>(false);
  readonly isDetailLoading = signal<boolean>(false);
  readonly hasError = signal<boolean>(false);
  readonly isForbidden = signal<boolean>(false);
  readonly errorMessage = signal<string>('');

  readonly incidents = signal<IncidentReportDto[]>([]);
  readonly totalRecords = signal<number>(0);
  readonly drivers = signal<any[]>([]);
  readonly vehicles = signal<VehicleDto[]>([]);

  // Filter signals
  readonly selectedDriverId = signal<string>('');
  readonly selectedVehicleId = signal<string>('');
  readonly selectedSeverity = signal<string>('');
  readonly fromDate = signal<string>('');
  readonly toDate = signal<string>('');
  readonly searchTerm = signal<string>('');
  readonly currentPage = signal<number>(1);
  readonly pageSize = signal<number>(20);

  // Dialog signals
  readonly isReportModalOpen = signal<boolean>(false);
  readonly isDetailModalOpen = signal<boolean>(false);
  readonly activeIncidentDetail = signal<IncidentReportDetailDto | null>(null);

  // Report form state
  newDriverId = '';
  newVehicleId = '';
  newOccurredAt = '';
  newLocation = '';
  newSeverity: IncidentSeverity = 'Minor';
  newDescription = '';
  newThirdPartyInfo = '';
  newPhotoKeys = '';
  reportError = signal<string>('');
  isSubmittingReport = signal<boolean>(false);

  readonly totalPages = computed(() => {
    return Math.max(1, Math.ceil(this.totalRecords() / this.pageSize()));
  });

  ngOnInit(): void {
    this.loadDriversAndVehicles();
    this.loadIncidents();
  }

  loadDriversAndVehicles(): void {
    this.incidentsService.getDrivers().subscribe({
      next: (res) => this.drivers.set(res.items || []),
      error: () => {},
    });
    this.incidentsService.getVehicles().subscribe({
      next: (res) => this.vehicles.set(res.items || []),
      error: () => {},
    });
  }

  loadIncidents(): void {
    this.isLoading.set(true);
    this.hasError.set(false);
    this.isForbidden.set(false);
    this.errorMessage.set('');

    this.incidentsService
      .getIncidents({
        driverId: this.selectedDriverId() || undefined,
        vehicleId: this.selectedVehicleId() || undefined,
        severity: (this.selectedSeverity() as IncidentSeverity) || undefined,
        fromDate: this.fromDate() || undefined,
        toDate: this.toDate() || undefined,
        searchTerm: this.searchTerm().trim() || undefined,
        page: this.currentPage(),
        pageSize: this.pageSize(),
      })
      .subscribe({
        next: (res) => {
          this.incidents.set(res.items || []);
          this.totalRecords.set(res.totalCount || 0);
          this.isLoading.set(false);
        },
        error: (err) => {
          this.isLoading.set(false);
          if (err.status === 403) {
            this.isForbidden.set(true);
          } else {
            this.hasError.set(true);
            this.errorMessage.set(err.message || 'Failed to load incidents.');
          }
        },
      });
  }

  applyFilters(): void {
    this.currentPage.set(1);
    this.loadIncidents();
  }

  resetFilters(): void {
    this.selectedDriverId.set('');
    this.selectedVehicleId.set('');
    this.selectedSeverity.set('');
    this.fromDate.set('');
    this.toDate.set('');
    this.searchTerm.set('');
    this.currentPage.set(1);
    this.loadIncidents();
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages() && page !== this.currentPage()) {
      this.currentPage.set(page);
      this.loadIncidents();
    }
  }

  // Details dialog
  viewDetails(incident: IncidentReportDto): void {
    this.isDetailLoading.set(true);
    this.isDetailModalOpen.set(true);

    this.incidentsService.getIncidentById(incident.id).subscribe({
      next: (detail) => {
        this.activeIncidentDetail.set(detail);
        this.isDetailLoading.set(false);
      },
      error: () => {
        this.activeIncidentDetail.set({
          ...incident,
          photoUrls: [],
        });
        this.isDetailLoading.set(false);
      },
    });
  }

  closeDetailModal(): void {
    this.isDetailModalOpen.set(false);
    this.activeIncidentDetail.set(null);
  }

  // Report modal
  openReportModal(): void {
    this.newDriverId = '';
    this.newVehicleId = '';
    this.newOccurredAt = this.formatForDateTimeLocal(new Date().toISOString());
    this.newLocation = '';
    this.newSeverity = 'Minor';
    this.newDescription = '';
    this.newThirdPartyInfo = '';
    this.newPhotoKeys = '';
    this.reportError.set('');
    this.isReportModalOpen.set(true);
  }

  closeReportModal(): void {
    this.isReportModalOpen.set(false);
  }

  submitReport(): void {
    if (!this.newVehicleId || !this.newOccurredAt || !this.newLocation.trim() || !this.newDescription.trim()) {
      this.reportError.set('Please fill in all mandatory fields.');
      return;
    }

    this.isSubmittingReport.set(true);
    this.reportError.set('');

    const photoKeyList = this.newPhotoKeys
      ? this.newPhotoKeys.split(',').map((k) => k.trim()).filter((k) => k.length > 0)
      : [];

    const request: ReportIncidentRequest = {
      driverId: this.newDriverId || null,
      vehicleId: this.newVehicleId,
      occurredAt: new Date(this.newOccurredAt).toISOString(),
      location: this.newLocation.trim(),
      severity: this.newSeverity,
      description: this.newDescription.trim(),
      thirdPartyInfo: this.newThirdPartyInfo.trim() || null,
      photoKeys: photoKeyList.length > 0 ? photoKeyList : null,
    };

    this.incidentsService.reportIncident(request).subscribe({
      next: () => {
        this.isSubmittingReport.set(false);
        this.closeReportModal();
        this.loadIncidents();
      },
      error: (err) => {
        this.isSubmittingReport.set(false);
        this.reportError.set(err.error?.message || err.message || 'Failed to submit incident report.');
      },
    });
  }

  private formatForDateTimeLocal(isoString: string): string {
    const d = new Date(isoString);
    if (isNaN(d.getTime())) return '';
    const pad = (n: number) => n.toString().padStart(2, '0');
    const y = d.getFullYear();
    const m = pad(d.getMonth() + 1);
    const day = pad(d.getDate());
    const h = pad(d.getHours());
    const min = pad(d.getMinutes());
    return `${y}-${m}-${day}T${h}:${min}`;
  }
}
