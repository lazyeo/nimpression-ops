# Ordinary NZ payroll settlement, 2026/27

`PayrollSettlementCalculator` is separate from gross earnings calculation. All amounts are NZD.
The caller must supply ordinary period earnings, verified declarations effective on the actual
pay date, and an explicit employee or contractor profile. No tax code, contribution status,
withholding exemption, GST status or holiday arrangement is inferred from a job title or defaults.
Only pay dates from 1 April 2026 through 31 March 2027 are supported.

Sources reviewed:

- [IRD payroll calculations/business rules, 2026/27, v1.01, 24 March 2026](https://www.ird.govt.nz/-/media/project/ir/home/documents/digital-service-providers/software-providers/payroll-calculations-business-rules-specifications/payroll-calculations-and-business-rules-specification.pdf?modified=20260401012317): sections 5.2–5.6, 5.14.2, 5.20.6 and 5.21.3.
- [IRD IR340 deduction tables, April 2026](https://www.ird.govt.nz/-/media/project/ir/home/documents/forms-and-guides/ir300---ir399/ir340/ir340-apr-2026.pdf?modified=20260323203256): independent weekly $1,000 row, M PAYE $171.50, ME $161.50, student loan $64.32, 3.5% employee contribution $35.
- [Employment NZ pay-as-you-go annual holiday payments](https://www.employment.govt.nz/pay-and-hours/pay-and-wages/leave-and-holiday-pay/pay-as-you-go-annual-holiday-payments): Hemi example, $862.50 ordinary gross plus $69 holiday pay gives $931.50.

Implementation boundaries and arithmetic:

- M, M SL, ME, ME SL and the SB/S/SH/ST/SA secondary codes with or without SL are supported. ME selection is the caller's verified IETC declaration, not an automatic income-only eligibility decision.
- Primary PAYE annualises and truncates earnings to whole dollars, applies annual tax, IETC where declared, and 1.75% ACC (annual cap $2,741.22 at $156,641). It truncates weekly PAYE to cents **before** converting and truncating to the pay frequency. Secondary PAYE uses the published flat rate including ACC on whole-dollar period earnings; the primary annualised cap is not substituted for the secondary rules.
- PAYE includes ACC. The displayed levy uses the same period conversion and truncation; displayed income tax is the residual PAYE minus levy so the breakdown reconciles exactly. This is an allocation of rounding, not a second deduction. Student loan and employee KiwiSaver are separate deductions.
- Student loan uses whole-dollar earnings and the published period threshold for primary codes, zero threshold for secondary codes, then truncates the 12% deduction to cents. KiwiSaver contributions use full earnings including cents, truncated to cents. Employer contributions are additional to wages; ESCT uses the whole-dollar gross contribution and truncates tax to cents.
- ESCT rates must be verified externally using the applicable previous-year or estimated earnings including employer contributions. This calculator does not infer a rate from one period's wages. Employee 3% requires an approved temporary reduction; zero requires an explicit non-contributing configuration. Employer rates must meet the declared compulsory obligation; voluntary contributions may continue when not compulsory.
- OrdinaryAccrual adds no automatic 8% cash payment. It does **not** calculate leave entitlement, balances or annual leave taken. PAYG requires a verified genuine fixed term below twelve months, or genuinely irregular work making annual holidays impracticable, plus the employment agreement. The 8% supplement is separately identified and rounded upward to the next cent so it is never below the minimum. This cent policy is an application choice; the official example is exact cents.
- Contractor withholding uses GST-exclusive ordinary invoice earnings and the explicitly verified rate/exemption. GST is separately added at the explicitly verified zero or 15% rate and rounded to nearest cent, midpoint away from zero. Contractor employee PAYE/ACC/KiwiSaver/ESCT/student-loan/holiday fields are zero because those employee deductions do not apply. This is payment settlement, not the contractor's final income tax or separate ACC liability.
- This version does not support extra pay, bonuses, termination pay, multiple-period/irregular pay, special/tailored tax or student-loan certificates, child support, extra student-loan deductions, other deductions, employee share schemes, non-cash benefits, salary sacrifice or employer contributions taxed as salary/PAYE. Such earnings must not be submitted under the ordinary-earnings contract. Unknown codes, dates and missing configurations return explicit validation issues and no calculation.
- Saved gross pay is not retrospectively repaired here. Minimum wage compliance and the legal Holidays Act gross-earnings base must be established by the caller before settlement. EmployerCost excludes employer work levies and other costs not modelled; contractor GST is included as cash outlay, without assuming input-tax recovery.

Some examples retained in the IRD PDF use earlier-year assumptions (notably the $500.03 PAYE example in the alternative employer-PAYE section). Tests use the current rules and current IR340 table instead of adopting those stale totals.
