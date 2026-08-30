import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { SupportedLang } from '../../../core/models/i18n.models';

@Component({
  selector: 'nim-login',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, I18nPipe],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly i18n = inject(I18nService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly isSubmitting = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly rateLimitSeconds = signal<number | null>(null);

  readonly loginForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]],
    rememberMe: [true],
  });

  get currentLang(): SupportedLang {
    return this.i18n.currentLang();
  }

  toggleLanguage(): void {
    const nextLang: SupportedLang = this.currentLang === 'en-NZ' ? 'zh-CN' : 'en-NZ';
    void this.i18n.setLanguage(nextLang);
  }

  onSubmit(): void {
    if (this.loginForm.invalid || this.isSubmitting()) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set(null);
    this.rateLimitSeconds.set(null);

    const { email, password } = this.loginForm.getRawValue();

    this.authService.login({ email, password }).subscribe({
      next: (res) => {
        this.isSubmitting.set(false);
        const returnUrl = this.route.snapshot.queryParams['returnUrl'];

        if (returnUrl && !returnUrl.startsWith('/auth')) {
          void this.router.navigateByUrl(returnUrl);
        } else if (res.user.role === 'Driver') {
          void this.router.navigate(['/driver']);
        } else {
          void this.router.navigate(['/admin']);
        }
      },
      error: (err) => {
        this.isSubmitting.set(false);
        if (err?.status === 429) {
          const retryAfter = err?.headers?.get('Retry-After');
          const seconds = retryAfter ? parseInt(retryAfter, 10) : 60;
          this.rateLimitSeconds.set(seconds);
          this.errorMessage.set(this.i18n.translate('AUTH.RATE_LIMIT_EXCEEDED', { seconds }));
        } else if (err?.status === 401) {
          this.errorMessage.set(this.i18n.translate('AUTH.INVALID_CREDENTIALS'));
        } else {
          this.errorMessage.set(err?.error?.detail || this.i18n.translate('COMMON.ERROR'));
        }
      },
    });
  }
}
