export enum NewsAudience {
  All = 1,
  Drivers = 2,
  Dispatchers = 3,
}

export interface NewsPostListItemDto {
  id: string;
  title: string;
  audience: NewsAudience | number;
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
  audience: NewsAudience | number;
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
  audience: number;
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
  role: string | number;
  employeeNo?: string | null;
}

export interface NewsListFilter {
  audience?: number | null;
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
