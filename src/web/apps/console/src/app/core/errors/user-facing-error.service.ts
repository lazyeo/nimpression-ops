import { inject, Injectable } from '@angular/core';
import { I18nService } from '../i18n/i18n.service';
import { resolveUserFacingError } from './user-facing-error';

@Injectable({ providedIn: 'root' })
export class UserFacingErrorService {
  private readonly i18n = inject(I18nService);

  format(error: unknown): string {
    const safe = resolveUserFacingError(error);
    return `${this.i18n.translate(safe.messageKey)} (${safe.code})`;
  }
}
