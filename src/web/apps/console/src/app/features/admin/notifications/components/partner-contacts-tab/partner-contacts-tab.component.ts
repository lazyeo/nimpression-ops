import {
  Component,
  ChangeDetectionStrategy,
  OnInit,
  signal,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { I18nPipe } from '../../../../../core/i18n/i18n.pipe';
import { IconComponent } from '../../../../../shared/components/icon/icon.component';
import { NotificationService } from '../../services/notification.service';
import {
  PartnerContactDto,
  PartnerContactFilter,
  PartnerKind,
  PagedResult,
} from '../../models/notification.models';

@Component({
  selector: 'nim-partner-contacts-tab',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, I18nPipe, IconComponent],
  templateUrl: './partner-contacts-tab.component.html',
  styleUrls: ['./partner-contacts-tab.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PartnerContactsTabComponent implements OnInit {
  private readonly notificationService = inject(NotificationService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly partners = signal<PartnerContactDto[]>([]);
  readonly totalCount = signal(0);
  readonly totalPages = signal(1);
  readonly currentPage = signal(1);

  // Filters
  readonly kindFilter = signal<number | null>(null);
  readonly activeFilter = signal<boolean | null>(null);
  readonly searchTerm = signal<string>('');

  // Dialog State
  readonly showEditDialog = signal(false);
  readonly isEditing = signal(false);
  readonly currentEditingId = signal<string | null>(null);
  readonly dialogSubmitting = signal(false);
  readonly dialogError = signal<string | null>(null);

  // Delete Confirmation State
  readonly deleteTarget = signal<PartnerContactDto | null>(null);
  readonly deleting = signal(false);

  readonly partnerForm = this.fb.group({
    kind: [PartnerKind.Insurer, [Validators.required]],
    companyName: ['', [Validators.required, Validators.maxLength(150)]],
    email: ['', [Validators.required, Validators.email]],
    active: [true],
  });

  ngOnInit(): void {
    this.loadPartners();
  }

  loadPartners(): void {
    this.loading.set(true);
    this.error.set(null);

    const filter: PartnerContactFilter = {
      kind: this.kindFilter() as PartnerKind | null,
      active: this.activeFilter(),
      searchTerm: this.searchTerm() || null,
      page: this.currentPage(),
      pageSize: 20,
    };

    this.notificationService.getPartners(filter).subscribe({
      next: (res: PagedResult<PartnerContactDto>) => {
        this.partners.set(res.items || []);
        this.totalCount.set(res.totalCount || 0);
        this.totalPages.set(res.totalPages || 1);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.message || 'NOTIFICATIONS.PARTNERS_LOAD_FAILED');
      },
    });
  }

  onFilterChange(): void {
    this.currentPage.set(1);
    this.loadPartners();
  }

  openCreateDialog(): void {
    this.isEditing.set(false);
    this.currentEditingId.set(null);
    this.dialogError.set(null);
    this.partnerForm.reset({
      kind: PartnerKind.Insurer,
      companyName: '',
      email: '',
      active: true,
    });
    this.showEditDialog.set(true);
  }

  openEditDialog(partner: PartnerContactDto): void {
    this.isEditing.set(true);
    this.currentEditingId.set(partner.id);
    this.dialogError.set(null);
    this.partnerForm.patchValue({
      kind: partner.kind,
      companyName: partner.companyName,
      email: partner.email,
      active: partner.active,
    });
    this.showEditDialog.set(true);
  }

  closeDialog(): void {
    this.showEditDialog.set(false);
    this.dialogError.set(null);
  }

  submitPartnerForm(): void {
    if (this.partnerForm.invalid) {
      this.partnerForm.markAllAsTouched();
      return;
    }

    this.dialogSubmitting.set(true);
    this.dialogError.set(null);
    const val = this.partnerForm.getRawValue();

    if (this.isEditing() && this.currentEditingId()) {
      this.notificationService
        .updatePartner(this.currentEditingId()!, {
          kind: Number(val.kind),
          companyName: val.companyName || '',
          email: val.email || '',
        })
        .subscribe({
          next: () => {
            this.dialogSubmitting.set(false);
            this.showEditDialog.set(false);
            this.loadPartners();
          },
          error: (err) => {
            this.dialogSubmitting.set(false);
            const detail = err.error?.detail || err.error?.message || err.message || 'NOTIFICATIONS.SAVE_PARTNER_FAILED';
            this.dialogError.set(detail);
          },
        });
    } else {
      this.notificationService
        .createPartner({
          kind: Number(val.kind),
          companyName: val.companyName || '',
          email: val.email || '',
          active: val.active ?? true,
        })
        .subscribe({
          next: () => {
            this.dialogSubmitting.set(false);
            this.showEditDialog.set(false);
            this.loadPartners();
          },
          error: (err) => {
            this.dialogSubmitting.set(false);
            const detail = err.error?.detail || err.error?.message || err.message || 'NOTIFICATIONS.SAVE_PARTNER_FAILED';
            this.dialogError.set(detail);
          },
        });
    }
  }

  togglePartnerActive(partner: PartnerContactDto, event: MouseEvent): void {
    event.stopPropagation();
    const action$ = partner.active
      ? this.notificationService.deactivatePartner(partner.id)
      : this.notificationService.activatePartner(partner.id);

    action$.subscribe({
      next: () => {
        this.partners.update((list) =>
          list.map((p) => (p.id === partner.id ? { ...p, active: !partner.active } : p)),
        );
      },
    });
  }

  confirmDelete(partner: PartnerContactDto, event: MouseEvent): void {
    event.stopPropagation();
    this.deleteTarget.set(partner);
  }

  cancelDelete(): void {
    this.deleteTarget.set(null);
  }

  executeDelete(): void {
    const target = this.deleteTarget();
    if (!target) return;

    this.deleting.set(true);
    this.notificationService.deletePartner(target.id).subscribe({
      next: () => {
        this.deleting.set(false);
        this.deleteTarget.set(null);
        this.loadPartners();
      },
      error: (err) => {
        this.deleting.set(false);
        this.error.set(err.message || 'NOTIFICATIONS.DELETE_PARTNER_FAILED');
      },
    });
  }
}
