# Driver tax settings — 12 September 2026

Drivers can open **Profile → Tax and KiwiSaver** to submit their declared employee tax code and KiwiSaver choices, or verified contractor withholding/GST arrangements. Administrators open **Payroll → Tax settings review** to review requests, confirm employer contributions/ESCT and holiday arrangements, and approve an effective date.

## Behaviour

- Personal choices begin unset. A driver must explicitly confirm a declaration. Only one pending request is allowed; it can be withdrawn before another is submitted.
- Approval cannot precede the requested date or the current New Zealand date. Supported effective dates end on 31 March 2027. Approved records are immutable, with one approval per driver and effective date.
- In an unfinalised payslip, entering a pay date resolves the latest approved profile applicable on that date. **Calculate using approved settings** explicitly calculates and saves the settlement. The server checks the selected profile again, and the settlement snapshot records its profile ID.
- Pending, rejected, withdrawn and future profiles cannot supply current deductions. Finalised or historical settlements are not rewritten.
- Driver access is restricted to their own records. Only administrators can review or use profiles for settlement. Tax-profile responses use `Cache-Control: no-store`; declarations are not placed in the offline operation queue or browser read cache. Audit entries record the action and identity, without declaration contents.
- English and Chinese labels and New Zealand dates are provided. Errors use readable messages and stable support codes.

## Data and rollout

Migration `20260912095056_AddDriverTaxProfiles` adds a new table and its ownership, pending-request and approval-date constraints. It does not populate personal declarations or update existing payroll data. The existing explicit development seed reset clears this new dependent table before recreating users and drivers.

## Limits

This feature records declarations and approval; it does not replace IR330/KS2 or other required evidence, collect IRD numbers/documents, independently verify tax eligibility, submit to IRD, or execute payments. It does not automatically settle an entire pay period without an explicit pay date and calculation action. Existing [ordinary payroll limits](2026-09-09-known-limits.md) remain.

An existing development reset limitation was observed: `cleanExisting` cannot delete users referenced by immutable audit history because the audit actor foreign key attempts an update. This feature does not disable or bypass the audit protection. The additive deployment migration does not invoke this reset.

Declaration guidance: [IRD deductions](https://www.ird.govt.nz/deductions), [IRD KiwiSaver employee deductions](https://www.ird.govt.nz/kiwisaver/kiwisaver-employers/contributions-and-deductions/kiwisaver-deductions-from-employee-pay).

## Validation

- Five focused domain tests and eight real PostgreSQL/API integration tests passed, covering ownership, explicit declarations, concurrent submissions, approval dates, stale selection, snapshot provenance, finalisation, contractor amounts, audit privacy and audit-free seed cleanup.
- All 421 frontend tests passed. Production build and all 12 guards passed.
- Built application checked with synthetic HTTP fixtures at 320/375 widths in English/Chinese for driver submission and administrator approval. This is browser automation, not a physical Android acceptance test or a live tax declaration.
