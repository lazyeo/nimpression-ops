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
import { AuthService } from '../../../../../core/auth/auth.service';
import { NotificationService } from '../../services/notification.service';
import {
  EmailTemplateDto,
  EmailTemplateFilter,
  KNOWN_TEMPLATE_KEYS,
  PagedResult,
} from '../../models/notification.models';

@Component({
  selector: 'nim-email-templates-tab',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, I18nPipe, IconComponent],
  templateUrl: './email-templates-tab.component.html',
  styleUrls: ['./email-templates-tab.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmailTemplatesTabComponent implements OnInit {
  readonly authService = inject(AuthService);
  private readonly notificationService = inject(NotificationService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly templates = signal<EmailTemplateDto[]>([]);
  readonly totalCount = signal(0);
  readonly searchTerm = signal<string>('');
  readonly knownKeys = KNOWN_TEMPLATE_KEYS;

  // Dialog State
  readonly showEditDialog = signal(false);
  readonly isEditing = signal(false);
  readonly currentEditingId = signal<string | null>(null);
  readonly dialogSubmitting = signal(false);
  readonly dialogError = signal<string | null>(null);

  readonly templateForm = this.fb.group({
    key: ['', [Validators.required]],
    subjectEn: ['', [Validators.required]],
    subjectZh: ['', [Validators.required]],
    bodyEn: ['', [Validators.required]],
    bodyZh: ['', [Validators.required]],
    active: [true],
  });

  ngOnInit(): void {
    this.loadTemplates();
  }

  loadTemplates(): void {
    this.loading.set(true);
    this.error.set(null);

    const filter: EmailTemplateFilter = {
      searchTerm: this.searchTerm() || null,
      page: 1,
      pageSize: 50,
    };

    this.notificationService.getTemplates(filter).subscribe({
      next: (res: PagedResult<EmailTemplateDto>) => {
        this.templates.set(res.items || []);
        this.totalCount.set(res.totalCount || 0);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.message || 'NOTIFICATIONS.TEMPLATES_LOAD_FAILED');
      },
    });
  }

  openCreateDialog(): void {
    this.isEditing.set(false);
    this.currentEditingId.set(null);
    this.dialogError.set(null);
    this.templateForm.reset({
      key: '',
      subjectEn: '',
      subjectZh: '',
      bodyEn: '',
      bodyZh: '',
      active: true,
    });
    this.showEditDialog.set(true);
  }

  openEditDialog(template: EmailTemplateDto): void {
    this.isEditing.set(true);
    this.currentEditingId.set(template.id);
    this.dialogError.set(null);
    this.templateForm.patchValue({
      key: template.key,
      subjectEn: template.subjectEn,
      subjectZh: template.subjectZh,
      bodyEn: template.bodyEn,
      bodyZh: template.bodyZh,
      active: template.active,
    });
    this.showEditDialog.set(true);
  }

  closeDialog(): void {
    this.showEditDialog.set(false);
    this.dialogError.set(null);
  }

  insertPlaceholder(placeholder: string, field: 'subjectEn' | 'subjectZh' | 'bodyEn' | 'bodyZh'): void {
    const current = this.templateForm.get(field)?.value || '';
    const tag = `{{${placeholder}}}`;
    this.templateForm.get(field)?.setValue(`${current} ${tag}`.trim());
  }

  getPlaceholdersForKey(key: string): string[] {
    const found = this.knownKeys.find((k) => k.key.toUpperCase() === key.toUpperCase());
    return found ? found.placeholders : [];
  }

  submitTemplateForm(): void {
    if (this.templateForm.invalid) {
      this.templateForm.markAllAsTouched();
      return;
    }

    this.dialogSubmitting.set(true);
    this.dialogError.set(null);
    const val = this.templateForm.getRawValue();

    if (this.isEditing() && this.currentEditingId()) {
      this.notificationService
        .updateTemplate(this.currentEditingId()!, {
          subjectEn: val.subjectEn || '',
          subjectZh: val.subjectZh || '',
          bodyEn: val.bodyEn || '',
          bodyZh: val.bodyZh || '',
        })
        .subscribe({
          next: () => {
            this.dialogSubmitting.set(false);
            this.showEditDialog.set(false);
            this.loadTemplates();
          },
          error: (err) => {
            this.dialogSubmitting.set(false);
            const detail = err.error?.detail || err.error?.message || err.message || 'NOTIFICATIONS.SAVE_TEMPLATE_FAILED';
            this.dialogError.set(detail);
          },
        });
    } else {
      this.notificationService
        .createTemplate({
          key: (val.key || '').trim().toUpperCase(),
          subjectEn: val.subjectEn || '',
          subjectZh: val.subjectZh || '',
          bodyEn: val.bodyEn || '',
          bodyZh: val.bodyZh || '',
          active: val.active ?? true,
        })
        .subscribe({
          next: () => {
            this.dialogSubmitting.set(false);
            this.showEditDialog.set(false);
            this.loadTemplates();
          },
          error: (err) => {
            this.dialogSubmitting.set(false);
            const detail = err.error?.detail || err.error?.message || err.message || 'NOTIFICATIONS.SAVE_TEMPLATE_FAILED';
            this.dialogError.set(detail);
          },
        });
    }
  }

  toggleTemplateActive(template: EmailTemplateDto, event: MouseEvent): void {
    event.stopPropagation();
    const action$ = template.active
      ? this.notificationService.deactivateTemplate(template.id)
      : this.notificationService.activateTemplate(template.id);

    action$.subscribe({
      next: () => {
        this.templates.update((list) =>
          list.map((t) => (t.id === template.id ? { ...t, active: !template.active } : t)),
        );
      },
    });
  }
}
