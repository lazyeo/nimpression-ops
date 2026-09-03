import type { NewsAudience, UserRole } from '../../../../core/api/models/api-models';

export type { NewsAudience } from '../../../../core/api/models/api-models';

export interface NewsPostListItemDto {
  id: string;
  title: string;
  audience: NewsAudience;
  publishedAt: string;
  pinned: boolean;
  isActive: boolean;
  isRead: boolean;
  readAt?: string | null;
}

export interface NewsPostDetailDto {
  id: string;
  authorUserId: string;
  authorDisplayName: string;
  title: string;
  bodyEn: string;
  bodyZh: string;
  audience: NewsAudience;
  publishedAt: string;
  pinned: boolean;
  isActive: boolean;
  isRead: boolean;
  readAt?: string | null;
}

export interface CreateNewsPostRequest {
  title: string;
  bodyEn: string;
  bodyZh: string;
  audience: NewsAudience;
  pinned?: boolean;
}

export interface NewsReadStatsDto {
  newsPostId: string;
  readCount: number;
  targetAudienceCount: number;
  readRate: number;
}

export interface UnreadUserDto {
  userId: string;
  displayName: string;
  email: string;
  role: UserRole;
  employeeNo?: string | null;
}

export interface NewsListFilter {
  audience?: NewsAudience | null;
  isPinned?: boolean | null;
  isActive?: boolean | null;
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
