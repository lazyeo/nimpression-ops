import { HttpErrorResponse } from '@angular/common/http';

export interface UserFacingError {
  code: string;
  titleKey: string;
  messageKey: string;
}

function presentation(code: string, message: string): UserFacingError {
  return { code, titleKey: 'ERRORS.TITLE', messageKey: `ERRORS.${message}` };
}

// ResultExtensions emits the machine code in ProblemDetails.title. Match only
// exact, known codes; never display server titles, details, URLs or validation text.
const businessErrors = new Map<string, UserFacingError>([
  ['missing_employer_reduction_declaration', presentation('PAYROLL-008', 'PAYROLL_EMPLOYER_REQUIRED')],
  ['payroll_minimum_rules_unsupported', presentation('PAYROLL-016', 'PAYROLL_MINIMUM_UNSUPPORTED')],
  ['payroll_settings_required', presentation('PAYROLL-001', 'PAYROLL_PROFILE_REQUIRED')],
  ['missing_worker_type', presentation('PAYROLL-001', 'PAYROLL_PROFILE_REQUIRED')],
  ['missing_employee_profile', presentation('PAYROLL-001', 'PAYROLL_PROFILE_REQUIRED')],
  ['missing_contractor_profile', presentation('PAYROLL-001', 'PAYROLL_PROFILE_REQUIRED')],
  ['conflicting_profile', presentation('PAYROLL-001', 'PAYROLL_PROFILE_REQUIRED')],
  ['unsupported_tax_year', presentation('PAYROLL-002', 'PAYROLL_UNSUPPORTED')],
  ['unsupported_pay_frequency', presentation('PAYROLL-015', 'PAYROLL_FREQUENCY_INVALID')],
  ['unsupported_tax_code', presentation('PAYROLL-002', 'PAYROLL_UNSUPPORTED')],
  ['payroll_currency_unsupported', presentation('PAYROLL-002', 'PAYROLL_UNSUPPORTED')],
  ['payslip_finalised', presentation('PAYROLL-003', 'PAYROLL_FINALISED')],
  ['period_finalised', presentation('PAYROLL-003', 'PAYROLL_FINALISED')],
  ['payroll_gross_recalculation_required', presentation('PAYROLL-004', 'PAYROLL_RECALCULATE')],
  ['payroll_settlement_required', presentation('PAYROLL-005', 'PAYROLL_SETTLEMENT_REQUIRED')],
  ['payroll_settlement_conflict', presentation('PAYROLL-006', 'PAYROLL_CHANGED')],
  ['missing_kiwisaver_configuration', presentation('PAYROLL-007', 'PAYROLL_KIWISAVER_REQUIRED')],
  ['invalid_kiwisaver_rate', presentation('PAYROLL-007', 'PAYROLL_KIWISAVER_REQUIRED')],
  ['missing_employer_configuration', presentation('PAYROLL-008', 'PAYROLL_EMPLOYER_REQUIRED')],
  ['invalid_employer_rate', presentation('PAYROLL-008', 'PAYROLL_EMPLOYER_REQUIRED')],
  ['missing_verified_esct_rate', presentation('PAYROLL-008', 'PAYROLL_EMPLOYER_REQUIRED')],
  ['missing_holiday_configuration', presentation('PAYROLL-009', 'PAYROLL_HOLIDAY_REQUIRED')],
  ['holiday_pay_ineligible', presentation('PAYROLL-009', 'PAYROLL_HOLIDAY_REQUIRED')],
  ['missing_verified_withholding', presentation('PAYROLL-010', 'PAYROLL_WITHHOLDING_REQUIRED')],
  ['missing_verified_gst', presentation('PAYROLL-011', 'PAYROLL_GST_REQUIRED')],
  ['payroll_pay_date_invalid', presentation('PAYROLL-012', 'PAYROLL_PAY_DATE_INVALID')],
  ['invalid_gross_earnings', presentation('PAYROLL-013', 'PAYROLL_GROSS_INVALID')],
  ['payslip_not_found', presentation('PAYROLL-014', 'PAYROLL_NOT_FOUND')],
  ['pay_period_not_found', presentation('PAYROLL-014', 'PAYROLL_NOT_FOUND')],

  ['area_code_conflict', presentation('AREA-002', 'AREA_CODE_EXISTS')],
  ['area_has_active_assignments', presentation('AREA-003', 'AREA_ASSIGNED')],
  ['area_in_use', presentation('AREA-004', 'AREA_IN_USE')],
  ['area_assignment_overlap', presentation('AREA-005', 'AREA_OVERLAP')],
  ['vehicle_rego_conflict', presentation('VEHICLE-002', 'VEHICLE_REGO_EXISTS')],
  ['vehicle_already_assigned', presentation('VEHICLE-003', 'VEHICLE_ASSIGNED')],

  ['invalid_task_transition', presentation('TASK-001', 'TASK_STATE_CHANGED')],
  ['job_task_not_found', presentation('TASK-002', 'TASK_NOT_FOUND')],
  ['driver_id_required', presentation('DRIVER-001', 'DRIVER_REQUIRED')],
  ['driver_not_found', presentation('DRIVER-002', 'DRIVER_NOT_FOUND')],
  ['driver_profile_not_found', presentation('DRIVER-003', 'DRIVER_PROFILE_REQUIRED')],
  ['vehicle_not_found', presentation('VEHICLE-001', 'VEHICLE_NOT_FOUND')],
  ['area_not_found', presentation('AREA-001', 'AREA_NOT_FOUND')],
  ['invalid_fine_transition', presentation('FINE-001', 'FINE_STATE_CHANGED')],
  ['file_too_large', presentation('FILE-001', 'FILE_TOO_LARGE')],
  ['unsupported_media_type', presentation('FILE-002', 'FILE_TYPE_UNSUPPORTED')],
]);

/** Produces only application-owned text keys and stable support codes. */
export function resolveUserFacingError(error: unknown): UserFacingError {
  if (!(error instanceof HttpErrorResponse)) {
    return presentation('OPS-UNKNOWN', 'GENERIC');
  }

  if (error.status >= 400 && error.status < 500) {
    const payload: unknown = error.error;
    if (payload && typeof payload === 'object' && 'title' in payload) {
      const title: unknown = payload.title;
      const known = typeof title === 'string' ? businessErrors.get(title) : undefined;
      if (known) return { ...known };
    }
  }

  switch (error.status) {
    case 0:
      return presentation('OPS-NETWORK', 'NETWORK');
    case 401:
      return presentation('OPS-401', 'AUTH_REQUIRED');
    case 403:
      return presentation('OPS-403', 'FORBIDDEN');
    case 404:
      return presentation('OPS-404', 'NOT_FOUND');
    case 409:
      return presentation('OPS-409', 'CONFLICT');
    case 400:
      return presentation('OPS-400', 'VALIDATION');
    case 422:
      return presentation('OPS-422', 'VALIDATION');
    case 413:
      return presentation('FILE-001', 'FILE_TOO_LARGE');
    case 415:
      return presentation('FILE-002', 'FILE_TYPE_UNSUPPORTED');
    case 429:
      return presentation('OPS-429', 'RATE_LIMITED');
    default:
      return error.status >= 500 && error.status <= 599
        ? presentation('OPS-SERVICE', 'SERVICE_UNAVAILABLE')
        : presentation('OPS-UNKNOWN', 'GENERIC');
  }
}

/** Rehydrate only known presentation codes; stored text is never trusted. */
export function resolveUserFacingErrorCode(code: unknown): UserFacingError {
  const known = [...businessErrors.values(), ...httpPresentations].find(
    (item) => item.code === code,
  );
  return known ? { ...known } : presentation('OPS-UNKNOWN', 'GENERIC');
}

const httpPresentations = [0, 400, 401, 403, 404, 409, 413, 415, 422, 429, 500].map((status) =>
  resolveUserFacingError(new HttpErrorResponse({ status })),
);
