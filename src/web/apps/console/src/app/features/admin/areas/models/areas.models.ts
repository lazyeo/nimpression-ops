export interface AreaDto {
  id: string;
  name: string;
  code: string;
  description?: string | null;
  geoJson?: string | null;
  isActive: boolean;
}

export interface AreaDetailDto {
  id: string;
  name: string;
  code: string;
  description?: string | null;
  geoJson?: string | null;
  isActive: boolean;
  activeDriversCount: number;
}

export interface AreaAssignmentDto {
  id: string;
  areaId: string;
  areaName: string;
  areaCode: string;
  driverId: string;
  driverName?: string | null;
  driverEmployeeNo?: string | null;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isActive: boolean;
}

export interface AreaFilter {
  searchTerm?: string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
}

export interface CreateAreaRequest {
  name: string;
  code: string;
  description?: string;
  geoJson?: string;
  isActive?: boolean;
}

export interface UpdateAreaRequest {
  name: string;
  code: string;
  description?: string;
  geoJson?: string;
  isActive?: boolean;
}

export interface AssignDriverToAreaRequest {
  driverId: string;
  effectiveFrom: string;
  effectiveTo?: string;
}

export interface EndAreaAssignmentRequest {
  effectiveTo: string;
}

export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}
