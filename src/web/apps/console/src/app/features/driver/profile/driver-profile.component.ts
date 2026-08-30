import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../../../core/auth/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../../core/i18n/locale-date.pipe';
import { SupportedLang } from '../../../core/models/i18n.models';

export interface DriverProfileDto {
  id: string;
  displayName: string;
  email: string;
  phone: string;
  emergencyContact: string;
  employeeNo: string;
  licenceClass: string;
  licenceExpiry: string;
  locale: string;
}

@Component({
  selector: 'nim-driver-profile',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, I18nPipe, LocaleDatePipe],
  templateUrl: './driver-profile.component.html',
  styleUrl: './driver-profile.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DriverProfileComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly http = inject(HttpClient);
  readonly authService = inject(AuthService);
  readonly i18n = inject(I18nService);

  readonly profile = signal<DriverProfileDto | null>(null);
  readonly isSaving = signal<boolean>(false);
  readonly saveSuccess = signal<boolean>(false);

  readonly profileForm = this.fb.group({
    phone: [''],
    emergencyContact: [''],
    locale: ['en-NZ', [Validators.required]],
  });

  ngOnInit(): void {
    const user = this.authService.currentUser();
    if (user) {
      this.profile.set({
        id: user.id,
        displayName: user.displayName,
        email: user.email,
        phone: '+64 21 000 0000',
        emergencyContact: '+64 21 999 9999',
        employeeNo: 'EMP-001',
        licenceClass: 'Class 4 Heavy',
        licenceExpiry: '2027-12-31',
        locale: user.locale || this.i18n.currentLang(),
      });

      this.profileForm.patchValue({
        phone: '+64 21 000 0000',
        emergencyContact: '+64 21 999 9999',
        locale: user.locale || this.i18n.currentLang(),
      });
    }

    if (user?.id) {
      this.http.get<DriverProfileDto>(`/api/drivers/${user.id}`).subscribe({
        next: (data) => {
          this.profile.set(data);
          this.profileForm.patchValue({
            phone: data.phone,
            emergencyContact: data.emergencyContact,
            locale: data.locale || this.i18n.currentLang(),
          });
        },
        error: () => {
          // Keep current
        },
      });
    }
  }

  saveProfile(): void {
    const user = this.authService.currentUser();
    if (!user || this.profileForm.invalid) return;

    this.isSaving.set(true);
    this.saveSuccess.set(false);

    const { phone, emergencyContact, locale } = this.profileForm.getRawValue();
    const nextLang = (locale as SupportedLang) || 'en-NZ';

    this.authService.updateUserLocale(nextLang).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.saveSuccess.set(true);
        setTimeout(() => this.saveSuccess.set(false), 3000);
      },
      error: () => {
        this.isSaving.set(false);
      },
    });
  }
}
