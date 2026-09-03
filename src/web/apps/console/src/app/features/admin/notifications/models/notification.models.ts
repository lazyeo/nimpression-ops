export enum PartnerKind {
  Insurer = 1,
  Maintenance = 2,
  Inspection = 3,
}

export interface PartnerContactDto {
  id: string;
  kind: PartnerKind;
  companyName: string;
  email: string;
  active: boolean;
}

export interface PartnerContactFilter {
  kind?: PartnerKind | null;
  active?: boolean | null;
  searchTerm?: string | null;
  page?: number;
  pageSize?: number;
}

export interface CreatePartnerContactRequest {
  kind: PartnerKind;
  companyName: string;
  email: string;
  active?: boolean;
}

export interface UpdatePartnerContactRequest {
  kind: PartnerKind;
  companyName: string;
  email: string;
}

export interface EmailTemplateDto {
  id: string;
  key: string;
  subjectEn: string;
  subjectZh: string;
  bodyEn: string;
  bodyZh: string;
  active: boolean;
}

export interface EmailTemplateFilter {
  searchTerm?: string | null;
  active?: boolean | null;
  page?: number;
  pageSize?: number;
}

export interface CreateEmailTemplateRequest {
  key: string;
  subjectEn: string;
  subjectZh: string;
  bodyEn: string;
  bodyZh: string;
  active?: boolean;
}

export interface UpdateEmailTemplateRequest {
  subjectEn: string;
  subjectZh: string;
  bodyEn: string;
  bodyZh: string;
}

export interface EmailLogDto {
  id: string;
  templateKey: string;
  toAddress: string;
  subject: string;
  status: string;
  attempts: number;
  lastError?: string | null;
  sentAt?: string | null;
  triggeredBy: string;
  correlationId: string;
}

export interface EmailLogFilter {
  status?: string | null;
  templateKey?: string | null;
  toAddress?: string | null;
  correlationId?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
  searchTerm?: string | null;
  page?: number;
  pageSize?: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export const KNOWN_TEMPLATE_KEYS = [
  {
    key: 'SERVICE_DUE_REMINDER',
    labelKey: 'NOTIFICATIONS.TEMPLATE_SERVICE_DUE',
    placeholders: ['VehicleRego', 'CurrentOdometer'],
  },
  {
    key: 'COMPLIANCE_EXPIRY_WARNING',
    labelKey: 'NOTIFICATIONS.TEMPLATE_COMPLIANCE_EXPIRY',
    placeholders: ['ExpiryType', 'VehicleRego', 'ExpiryDate'],
  },
  {
    key: 'INCIDENT_NOTIFICATION',
    labelKey: 'NOTIFICATIONS.TEMPLATE_INCIDENT',
    placeholders: ['Severity', 'VehicleRego'],
  },
  {
    key: 'FINE_ACCEPTED_NOTICE',
    labelKey: 'NOTIFICATIONS.TEMPLATE_FINE_ACCEPTED',
    placeholders: ['FineRef'],
  },
];
