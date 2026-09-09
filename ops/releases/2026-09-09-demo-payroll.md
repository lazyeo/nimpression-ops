# Demonstration payroll settlement

The original 60 deterministic demonstration payslips predate settlement snapshots. Their missing deductions are not a zero-tax declaration.

The explicit demo repair uses the ordinary-payroll calculator with the following **synthetic assumptions**, not verified personal tax declarations:

- Employee, fortnightly NZD pay, tax code M, no student loan.
- Employee and additional employer KiwiSaver: 3.5%; assumed employer ESCT: 30%.
- Ordinary annual holiday accrual; no automatic 8% cash supplement or invented leave balance.
- Existing recorded payment date where present; otherwise the seeded scheduled date three days after the period ends. This does not mark an unpaid period paid.

Every generated snapshot is marked `IsDemo`; admin and driver details display a bilingual demonstration notice. This is not evidence of a real payroll payment.

Repair is separate from application startup and database migration. The default invocation is preview only. Applying requires an explicit flag and is restricted to exact seeded identifiers, relationships and unchanged seeded earnings/line data. Existing settlement snapshots and changed/non-seed records are protected. It does not infer actual worker tax settings.

Four original seed payslips used the old $23.15 adult minimum. With the separate legacy-minimum correction flag, only exact matching legacy seed records are corrected to the applicable $23.95 floor before calculating deductions. The expected gross adjustment is $267.20 across IDs ending 19, 29, 49 and 59. Original hours, rates, calculation/finalisation timestamps and recorded payment status are retained; the corresponding top-up line is corrected consistently.

This repair does not introduce reusable real-driver tax profiles, contractor demonstration histories, statutory filing or payment execution. See [the remaining coverage](2026-09-09-known-limits.md).

Operator commands (run with the application's normal database configuration):

```sh
Nimpression.Api repair-demo-payroll --repair-legacy-minimum
Nimpression.Api repair-demo-payroll --repair-legacy-minimum --apply
```

Inspect the first command's eligible, modified, existing and minimum-correction counts before applying. A successful application should be followed by an idempotence preview and driver API checks; do not bypass unmatched-record protection with manual broad SQL updates.
