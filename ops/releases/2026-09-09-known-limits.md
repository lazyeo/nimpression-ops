# 9 September 2026 release: remaining coverage

## Payroll

- Ordinary NZD settlement supports pay dates from 1 April 2026 to 31 March 2027 only. Other tax years require new rules and regression examples.
- Supported frequencies are weekly, fortnightly, four-weekly and full calendar months. Irregular, multiple-period and extra-pay calculations are not implemented.
- Annual holiday entitlement, opening/closing balances, leave requests, leave taken, OWP/AWE comparisons and final holiday pay are not calculated. Eligible, explicitly verified 8% pay-as-you-go supplements are supported; ordinary accrual is shown as a separate arrangement without a computed leave balance.
- Bonuses, redundancy/termination payments, tailored tax codes/certificates, tailored or additional student-loan notices, child support, other deductions, employee share schemes, non-cash benefits, salary sacrifice and employer contributions taxed through PAYE are not supported by this ordinary-earnings contract.
- Since the 12 September tax-settings change, drivers can submit effective-dated personal declarations and administrators can approve them with employer and holiday settings. An administrator explicitly selects a pay date and calculates an unfinalised payslip using the applicable approved profile; manual per-payslip entry remains available. Automatic IRD certificate/eligibility verification is not implemented. See [tax settings](2026-09-12-tax-settings.md).
- Contractor settlement does not calculate the contractor's final income tax or separate ACC invoice, or issue a complete tax invoice/credit note.
- Employer cost excludes employer ACC work levies and other unmodelled employer expenses.
- Minimum wage checks use adult rates only. Starting-out/training eligibility is not modelled. A pay period spanning a rate change must be split; automatic per-day rate splitting is not implemented.
- Historical non-demo payslips without a settlement snapshot retain unknown net pay/deductions. There is no automatic reconstruction from current personal tax settings. The separately invoked, strictly scoped demo repair can populate unchanged deterministic seed records with explicitly marked synthetic assumptions; see [demo payroll](2026-09-09-demo-payroll.md).
- Year-to-date totals, comprehensive leave balances, employer/employee IRD identity presentation, PDF payslip export and statutory payroll reporting are not supplied by this change.
- No IRD payday filing, bank payment execution, Xero synchronisation or accounting reconciliation is implemented by this release. A recorded payment status is not evidence of a bank transfer.
- Finalised settlements cannot be edited in place, but the existing void/reopen workflow is not a complete immutable revision archive.

## Driver, offline and data

- Task details show existing task descriptions and operational fields. Structured cargo line items, quantities, delivery documents, recipient signatures and photos require a separate data/workflow extension.
- Failed offline operations remain in the local queue until resolved; deployment does not discard them. Reconnection refresh and safe error messages are implemented, but automatic resolution of every stale business operation is not.
- Read caches are scoped to the signed-in account. The persisted operation queue has not been redesigned for per-account isolation in this change.
- No remote cleanup of a tester's browser storage is performed. Real-device offline/reconnect acceptance on the reported Android browser remains unverified; automated and local browser checks do not establish it.
- Two completed production test tasks retain future scheduled dates: TSK-20260915-BB5750 and TSK-20260920-E2E337. Actual completion dates are in the past. No replacement date or deletion has been inferred.
- New seed contacts no longer use technical prefixes. The explicit demo-contact repair command has not been executed against production; existing stored values are not silently rewritten.

## Verification before deployment

- Backend: 235 domain, 399 application and 183 integration tests passed, excluding the Timing category as in CI. Subsequent DTO route checks passed against PostgreSQL.
- Frontend: 395 tests passed. Production build and all 12 guards passed.
- Built Angular UI with synthetic API fixtures: employee and contractor settlement flows passed at EN 320/375/1280 and ZH 320 widths, with no overflow or JavaScript errors; amount wrapping, snapshot date and close control checked.
- These checks do not constitute live payroll payment, live IRD submission or exhaustive mobile-device acceptance.

Calculation scope and source references: [SettlementRules.md](../../src/server/Nimpression.Domain/Services/Payroll/SettlementRules.md).
