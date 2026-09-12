import type { SettlementRequest, WorkerType } from './settlement.models';
export type EmployeeTaxProfile = NonNullable<SettlementRequest['employee']>;
export type ContractorTaxProfile = NonNullable<SettlementRequest['contractor']>;
export type TaxProfileStatus = 'Pending' | 'Approved' | 'Rejected' | 'Withdrawn';
export interface TaxDeclaration {
  workerType: WorkerType;
  employee: Pick<EmployeeTaxProfile, 'taxCode' | 'kiwiSaverEmployee'> | null;
  contractor: ContractorTaxProfile | null;
}
export interface TaxProfile {
  id: string;
  driverId: string;
  driverName: string | null;
  employeeNo: string | null;
  effectiveFrom: string;
  status: TaxProfileStatus;
  declaration: TaxDeclaration;
  submittedAt: string;
  reviewedAt: string | null;
  approvedEmployee: EmployeeTaxProfile | null;
  approvedContractor: ContractorTaxProfile | null;
}
export interface TaxProfilePage {
  items: TaxProfile[];
  totalCount: number;
  page: number;
  pageSize: number;
}
export interface SubmitTaxProfile {
  effectiveFrom: string;
  declaration: TaxDeclaration;
  confirmed: true;
}
export interface ApproveTaxProfile {
  effectiveFrom: string;
  kiwiSaverEmployer: EmployeeTaxProfile['kiwiSaverEmployer'] | null;
  holidayPay: EmployeeTaxProfile['holidayPay'] | null;
  confirmed: true;
}
