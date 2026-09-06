import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { I18nService } from '../../../core/i18n/i18n.service';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { AuthService } from '../../../core/auth/auth.service';
import { SupportedLang } from '../../../core/models/i18n.models';

export type LanguageSwitchSize = 'sm' | 'md';

@Component({
  selector: 'nim-language-switch',
  standalone: true,
  imports: [CommonModule, I18nPipe],
  templateUrl: './language-switch.component.html',
  styleUrl: './language-switch.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LanguageSwitchComponent {
  private readonly i18n = inject(I18nService);
  private readonly authService = inject(AuthService);

  readonly size = input<LanguageSwitchSize>('md');

  readonly currentLang = computed<SupportedLang>(() => this.i18n.currentLang());

  selectLanguage(lang: SupportedLang): void {
    if (this.currentLang() === lang) {
      return;
    }

    if (this.authService.isAuthenticated()) {
      this.authService.updateUserLocale(lang).subscribe();
    } else {
      void this.i18n.setLanguage(lang);
    }
  }
}
