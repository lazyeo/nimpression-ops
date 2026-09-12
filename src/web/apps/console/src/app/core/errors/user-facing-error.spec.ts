import { HttpErrorResponse } from '@angular/common/http';
import { describe, expect, it } from 'vitest';
import { ErrorNotificationService } from '../services/error-notification.service';
import { resolveUserFacingError, resolveUserFacingErrorCode } from './user-facing-error';

const privateDetail = 'https://internal.example/api/private?token=secret SQL stack at Handler:42';

describe('user-facing error boundary', () => {
  it.each([
    ['tax_profile_confirmation_required', 'TAX-001', 'TAX_CONFIRMATION_REQUIRED'],
    ['tax_profile_effective_date_invalid', 'TAX-002', 'TAX_EFFECTIVE_DATE_INVALID'],
    ['tax_profile_declaration_required', 'TAX-003', 'TAX_DECLARATION_REQUIRED'],
    ['tax_profile_pending_exists', 'TAX-004', 'TAX_PENDING_EXISTS'],
    ['tax_profile_not_pending', 'TAX-005', 'TAX_NOT_PENDING'],
    ['tax_profile_effective_date_conflict', 'TAX-006', 'TAX_EFFECTIVE_DATE_CONFLICT'],
    ['tax_profile_not_found', 'TAX-007', 'TAX_NOT_FOUND'],
    ['tax_profile_status_invalid', 'TAX-008', 'TAX_STATUS_INVALID'],
    ['tax_profile_stale_selection', 'TAX-009', 'TAX_SELECTION_CHANGED'],
    ['tax_profile_conflict', 'TAX-010', 'TAX_SAVE_CONFLICT'],
  ])('maps tax settings error %s without exposing response data', (title, code, key) => {
    const safe = resolveUserFacingError(new HttpErrorResponse({
      status: 409, error: { title, detail: privateDetail }, url: privateDetail,
    }));
    expect(safe).toEqual({ code, titleKey: 'ERRORS.TITLE', messageKey: `ERRORS.${key}` });
    expect(JSON.stringify(safe)).not.toContain(privateDetail);
    expect(resolveUserFacingErrorCode(code)).toEqual(safe);
  });

  it('maps the actual task transition code without assuming that the task was cancelled', () => {
    const safe = resolveUserFacingError(
      new HttpErrorResponse({
        status: 422,
        url: privateDetail,
        error: { title: 'invalid_task_transition', detail: privateDetail },
      }),
    );
    expect(safe).toEqual({
      code: 'TASK-001',
      titleKey: 'ERRORS.TITLE',
      messageKey: 'ERRORS.TASK_STATE_CHANGED',
    });
  });

  it.each([
    {
      title: privateDetail,
      detail: privateDetail,
      message: privateDetail,
      errors: { password: [privateDetail] },
    },
    privateDetail,
    { title: 'constructor', code: privateDetail },
    { title: '__proto__' },
  ])('never echoes unknown response fields', (error) => {
    expect(
      resolveUserFacingError(
        new HttpErrorResponse({
          status: 400,
          statusText: privateDetail,
          url: privateDetail,
          error,
        }),
      ),
    ).toEqual({ code: 'OPS-400', titleKey: 'ERRORS.TITLE', messageKey: 'ERRORS.VALIDATION' });
  });

  it.each([
    [0, 'OPS-NETWORK', 'NETWORK'],
    [401, 'OPS-401', 'AUTH_REQUIRED'],
    [403, 'OPS-403', 'FORBIDDEN'],
    [404, 'OPS-404', 'NOT_FOUND'],
    [409, 'OPS-409', 'CONFLICT'],
    [422, 'OPS-422', 'VALIDATION'],
    [429, 'OPS-429', 'RATE_LIMITED'],
    [503, 'OPS-SERVICE', 'SERVICE_UNAVAILABLE'],
  ])('uses an owned fallback for HTTP %s', (status, code, key) => {
    expect(
      resolveUserFacingError(
        new HttpErrorResponse({ status: Number(status), error: privateDetail }),
      ),
    ).toEqual({ code, titleKey: 'ERRORS.TITLE', messageKey: `ERRORS.${key}` });
  });

  it('does not trust legacy strings or arbitrary objects as HTTP errors', () => {
    for (const error of [
      privateDetail,
      new Error(privateDetail),
      { status: 422, error: { title: 'invalid_task_transition' } },
    ]) {
      expect(resolveUserFacingError(error).code).toBe('OPS-UNKNOWN');
    }
  });

  it('rehydrates only allowlisted support codes and ignores stored display text', () => {
    expect(resolveUserFacingErrorCode('TASK-001').messageKey).toBe('ERRORS.TASK_STATE_CHANGED');
    expect(resolveUserFacingErrorCode('OPS-403').messageKey).toBe('ERRORS.FORBIDDEN');
    for (const code of [privateDetail, 'constructor', undefined, { code: 'TASK-001' }]) {
      expect(resolveUserFacingErrorCode(code).code).toBe('OPS-UNKNOWN');
    }
  });

  it('sanitizes callers that bypass the interceptor and preserves dismissal behavior', () => {
    const service = new ErrorNotificationService();
    service.showError({ title: privateDetail, detail: privateDetail, statusCode: 500 });
    const latest = service.latestError()!;
    expect(latest.title).toBe('ERRORS.TITLE');
    expect(latest.detail).toBe('ERRORS.GENERIC');
    expect(JSON.stringify(service.activeErrors())).not.toContain(privateDetail);
    service.dismissError(latest.id);
    expect(service.latestError()).toBeNull();
    expect(service.activeErrors()).toEqual([]);
  });
  it('maps payroll business codes without exposing profile fields or server details', () => {
    const safe = resolveUserFacingError(new HttpErrorResponse({
      status: 422,
      error: { title: 'missing_verified_esct_rate', detail: privateDetail, field: 'Employee.KiwiSaverEmployer.EsctRate' },
    }));
    expect(safe).toEqual({ code: 'PAYROLL-008', titleKey: 'ERRORS.TITLE', messageKey: 'ERRORS.PAYROLL_EMPLOYER_REQUIRED' });
    expect(resolveUserFacingErrorCode('PAYROLL-008')).toEqual(safe);
  });

});
