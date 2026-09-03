import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../../core/i18n/locale-date.pipe';
import { AuthService } from '../../../core/auth/auth.service';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { AreasService, DriverOption } from './services/areas.service';
import {
  AreaAssignmentDto,
  AreaDto,
  AreaFilter,
} from './models/areas.models';

export type ViewState = 'loading' | 'success' | 'empty' | 'error' | 'forbidden';

@Component({
  selector: 'nim-areas',
  standalone: true,
  imports: [CommonModule, FormsModule, I18nPipe, LocaleDatePipe, IconComponent],
  templateUrl: './areas.component.html',
  styleUrl: './areas.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AreasComponent implements OnInit {
  private readonly areasService = inject(AreasService);
  readonly auth = inject(AuthService);

  // States
  readonly state = signal<ViewState>('loading');
  readonly errorMessage = signal<string>('');
  readonly isSubmitting = signal<boolean>(false);
  readonly formError = signal<string | null>(null);

  // Data
  readonly areas = signal<AreaDto[]>([]);
  readonly drivers = signal<DriverOption[]>([]);
  readonly areaAssignments = signal<AreaAssignmentDto[]>([]);
  readonly allAssignments = signal<AreaAssignmentDto[]>([]);

  // Filters & Pagination
  readonly searchTerm = signal<string>('');
  readonly selectedStatus = signal<string>('');
  readonly currentPage = signal<number>(1);
  readonly pageSize = signal<number>(20);
  readonly totalCount = signal<number>(0);
  readonly totalPages = signal<number>(1);

  // Modals
  readonly isCreateModalOpen = signal<boolean>(false);
  readonly isEditModalOpen = signal<boolean>(false);
  readonly isAssignModalOpen = signal<boolean>(false);
  readonly isDriversModalOpen = signal<boolean>(false);
  readonly isDeleteModalOpen = signal<boolean>(false);

  readonly selectedArea = signal<AreaDto | null>(null);

  // Form States
  createForm = {
    name: '',
    code: '',
    description: '',
    isActive: true,
  };

  editForm = {
    name: '',
    code: '',
    description: '',
    isActive: true,
  };

  assignForm = {
    driverId: '',
    effectiveFrom: new Date().toISOString().slice(0, 10),
    effectiveTo: '',
  };

  ngOnInit(): void {
    if (!this.auth.isAdminOrDispatcher()) {
      this.state.set('forbidden');
      return;
    }

    this.loadDrivers();
    this.loadAssignments();
    this.loadAreas();
  }

  loadAreas(setLoading = true): void {
    if (setLoading) {
      this.state.set('loading');
    }
    this.errorMessage.set('');

    const filter: AreaFilter = {
      searchTerm: this.searchTerm() || undefined,
      isActive:
        this.selectedStatus() === 'true'
          ? true
          : this.selectedStatus() === 'false'
            ? false
            : undefined,
      page: this.currentPage(),
      pageSize: this.pageSize(),
    };

    this.areasService.getAreas(filter).subscribe({
      next: (res) => {
        this.areas.set(res.items || []);
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
          this.errorMessage.set(err.error?.message || err.message || 'Error loading areas');
        }
      },
    });
  }

  loadDrivers(): void {
    this.areasService.getDrivers().subscribe({
      next: (res) => this.drivers.set(res.items || []),
      error: () => {},
    });
  }

  loadAssignments(): void {
    this.areasService.getAllAreaAssignments().subscribe({
      next: (res) => this.allAssignments.set(res || []),
      error: () => {},
    });
  }

  getAreaDriverCount(areaId: string): number {
    return this.allAssignments().filter((a) => a.areaId === areaId && a.isActive).length;
  }

  onSearchInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.searchTerm.set(input.value);
    this.currentPage.set(1);
    this.loadAreas();
  }

  onStatusChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.selectedStatus.set(select.value);
    this.currentPage.set(1);
    this.loadAreas();
  }

  clearFilters(): void {
    this.searchTerm.set('');
    this.selectedStatus.set('');
    this.currentPage.set(1);
    this.loadAreas();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage.set(page);
    this.loadAreas();
  }

  // --- Modals ---

  openCreateModal(): void {
    this.createForm = {
      name: '',
      code: '',
      description: '',
      isActive: true,
    };
    this.formError.set(null);
    this.isCreateModalOpen.set(true);
  }

  closeCreateModal(): void {
    this.isCreateModalOpen.set(false);
  }

  submitCreateArea(): void {
    if (!this.createForm.name || !this.createForm.code) return;

    this.isSubmitting.set(true);
    this.formError.set(null);

    this.areasService
      .createArea({
        name: this.createForm.name.trim(),
        code: this.createForm.code.trim().toUpperCase(),
        description: this.createForm.description?.trim() || undefined,
        isActive: this.createForm.isActive,
      })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.closeCreateModal();
          this.loadAreas();
        },
        error: (err: HttpErrorResponse) => {
          this.isSubmitting.set(false);
          if (err.status === 409) {
            this.formError.set('Area code already exists. Please choose a unique code.');
          } else {
            this.formError.set(err.error?.message || err.error?.detail || err.message || 'Failed to create area');
          }
        },
      });
  }

  openEditModal(area: AreaDto): void {
    this.selectedArea.set(area);
    this.formError.set(null);
    this.editForm = {
      name: area.name,
      code: area.code,
      description: area.description || '',
      isActive: area.isActive,
    };
    this.isEditModalOpen.set(true);
  }

  closeEditModal(): void {
    this.isEditModalOpen.set(false);
    this.selectedArea.set(null);
  }

  submitEditArea(): void {
    const area = this.selectedArea();
    if (!area || !this.editForm.name || !this.editForm.code) return;

    this.isSubmitting.set(true);
    this.formError.set(null);

    this.areasService
      .updateArea(area.id, {
        name: this.editForm.name.trim(),
        code: this.editForm.code.trim().toUpperCase(),
        description: this.editForm.description?.trim() || undefined,
        isActive: this.editForm.isActive,
      })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.closeEditModal();
          this.loadAreas();
        },
        error: (err: HttpErrorResponse) => {
          this.isSubmitting.set(false);
          this.formError.set(err.error?.message || err.error?.detail || 'Failed to update area');
        },
      });
  }

  openAssignModal(area: AreaDto): void {
    this.selectedArea.set(area);
    this.assignForm = {
      driverId: this.drivers().length > 0 ? this.drivers()[0].id : '',
      effectiveFrom: new Date().toISOString().slice(0, 10),
      effectiveTo: '',
    };
    this.formError.set(null);
    this.isAssignModalOpen.set(true);
  }

  closeAssignModal(): void {
    this.isAssignModalOpen.set(false);
    this.selectedArea.set(null);
  }

  submitAssignDriver(): void {
    const area = this.selectedArea();
    if (!area || !this.assignForm.driverId || !this.assignForm.effectiveFrom) return;

    this.isSubmitting.set(true);
    this.formError.set(null);

    this.areasService
      .assignDriverToArea(area.id, {
        driverId: this.assignForm.driverId,
        effectiveFrom: this.assignForm.effectiveFrom,
        effectiveTo: this.assignForm.effectiveTo || undefined,
      })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.closeAssignModal();
          this.loadAssignments();
        },
        error: (err: HttpErrorResponse) => {
          this.isSubmitting.set(false);
          if (err.status === 422) {
            this.formError.set(
              err.error?.message ||
                err.error?.detail ||
                'Driver area assignment dates overlap with existing assignment.',
            );
          } else {
            this.formError.set(err.error?.message || err.error?.detail || 'Failed to assign driver to area');
          }
        },
      });
  }

  openDriversModal(area: AreaDto): void {
    this.selectedArea.set(area);
    this.areasService.getAreaAssignments(area.id).subscribe({
      next: (asgs) => this.areaAssignments.set(asgs || []),
      error: () => this.areaAssignments.set([]),
    });
    this.isDriversModalOpen.set(true);
  }

  closeDriversModal(): void {
    this.isDriversModalOpen.set(false);
    this.selectedArea.set(null);
    this.areaAssignments.set([]);
  }

  endAssignment(assignment: AreaAssignmentDto): void {
    const today = new Date().toISOString().slice(0, 10);
    this.areasService.endAreaAssignment(assignment.id, { effectiveTo: today }).subscribe({
      next: () => {
        const area = this.selectedArea();
        if (area) {
          this.openDriversModal(area);
        }
        this.loadAssignments();
      },
      error: (err: HttpErrorResponse) => {
        alert(err.error?.message || 'Failed to end assignment');
      },
    });
  }

  openDeleteModal(area: AreaDto): void {
    this.selectedArea.set(area);
    this.formError.set(null);
    this.isDeleteModalOpen.set(true);
  }

  closeDeleteModal(): void {
    this.isDeleteModalOpen.set(false);
    this.selectedArea.set(null);
  }

  submitDeleteArea(): void {
    const area = this.selectedArea();
    if (!area) return;

    this.isSubmitting.set(true);
    this.formError.set(null);

    this.areasService.deleteArea(area.id).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.closeDeleteModal();
        this.loadAreas();
      },
      error: (err: HttpErrorResponse) => {
        this.isSubmitting.set(false);
        if (err.status === 409) {
          this.formError.set('Cannot delete area with active driver assignments. Please end assignments first.');
        } else {
          this.formError.set(err.error?.message || err.error?.detail || 'Failed to delete area');
        }
      },
    });
  }
}
