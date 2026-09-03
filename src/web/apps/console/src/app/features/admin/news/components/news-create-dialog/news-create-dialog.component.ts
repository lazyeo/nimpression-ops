import {
  Component,
  ChangeDetectionStrategy,
  output,
  signal,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { I18nPipe } from '../../../../../core/i18n/i18n.pipe';
import { IconComponent } from '../../../../../shared/components/icon/icon.component';
import { NewsService } from '../../services/news.service';
import { CreateNewsPostRequest, NewsAudience } from '../../models/news.models';

@Component({
  selector: 'nim-news-create-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, I18nPipe, IconComponent],
  templateUrl: './news-create-dialog.component.html',
  styleUrls: ['./news-create-dialog.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewsCreateDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly newsService = inject(NewsService);

  readonly closeDialog = output<boolean>();
  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    audience: ['All' as NewsAudience, [Validators.required]],
    pinned: [false],
    bodyEn: ['', [Validators.required]],
    bodyZh: ['', [Validators.required]],
  });

  submitForm(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    const val = this.form.getRawValue();
    const req: CreateNewsPostRequest = {
      title: val.title || '',
      bodyEn: val.bodyEn || '',
      bodyZh: val.bodyZh || '',
      audience: (val.audience || 'All') as NewsAudience,
      pinned: val.pinned ?? false,
    };

    this.newsService.createNews(req).subscribe({
      next: () => {
        this.submitting.set(false);
        this.closeDialog.emit(true);
      },
      error: (err) => {
        this.submitting.set(false);
        const detail = err.error?.detail || err.error?.message || err.message || 'NEWS.CREATE_FAILED';
        this.errorMessage.set(detail);
      },
    });
  }

  cancel(): void {
    this.closeDialog.emit(false);
  }
}
