# Nimpression Ops — Seed Data Specification

Deterministic demo seed dataset providing 90 days of operational logistics data across all domain aggregates.

## Data Scale
- **Users**: 13 (1 Admin, 2 Dispatchers, 10 Drivers)
  - Default password: `Passw0rd!demo` (BCrypt)
- **Drivers**: 10 (Employee numbers `DRV-001` to `DRV-010`, licence classes 2, 4, 5)
  - Includes `DRV-009` ($22.00/hr) to exercise statutory minimum wage floor guard (`MinimumWageTopUp`).
- **Vehicles**: 11 heavy trucks and delivery vehicles
  - `NIM001` - `NIM010` (active commercial fleet) + `NIM011` (spare utility vehicle)
  - Includes expired COF (`NIM004`), 30-day COF warning (`NIM005`), 30-day insurance warning (`NIM006`), and service-due threshold trigger (`NIM003`).
- **Areas**: 6 Auckland & Waikato operational freight zones with polygon boundaries.
- **90-day History**:
  - Realistic weekday/weekend task density
  - Cross-midnight night shifts (22:00 - 06:00)
  - Daylight Saving Time (DST) crossing shifts
  - Odometer readings and planned vs actual distances
  - Traffic infringement fines across all review states
  - Safety incident reports with insurer notifications
  - Bilingual news announcements and driver read receipts
  - Bi-weekly pay periods and dual-basis payslips with line item breakdowns
  - Append-only audit events and transactional outbox messages.

## Execution
Run `task seed` from repository root.
Seed generation uses fixed RNG seed `42` ensuring 100% deterministic, reproducible data output.
