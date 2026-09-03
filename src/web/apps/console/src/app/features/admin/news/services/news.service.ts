import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  CreateNewsPostRequest,
  NewsListFilter,
  NewsPostDetailDto,
  NewsPostListItemDto,
  NewsReadStatsDto,
  PagedResult,
  UnreadUserDto,
} from '../models/news.models';

@Injectable({
  providedIn: 'root',
})
export class NewsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/news';

  getNews(filter?: NewsListFilter): Observable<PagedResult<NewsPostListItemDto>> {
    let params = new HttpParams();
    if (filter?.audience !== undefined && filter?.audience !== null) {
      params = params.set('audience', filter.audience.toString());
    }
    if (filter?.isPinned !== undefined && filter?.isPinned !== null) {
      params = params.set('isPinned', filter.isPinned.toString());
    }
    if (filter?.isActive !== undefined && filter?.isActive !== null) {
      params = params.set('isActive', filter.isActive.toString());
    }
    if (filter?.page) {
      params = params.set('page', filter.page.toString());
    }
    if (filter?.pageSize) {
      params = params.set('pageSize', filter.pageSize.toString());
    }

    return this.http.get<PagedResult<NewsPostListItemDto>>(this.baseUrl, { params });
  }

  getNewsById(id: string): Observable<NewsPostDetailDto> {
    return this.http.get<NewsPostDetailDto>(`${this.baseUrl}/${id}`);
  }

  createNews(request: CreateNewsPostRequest): Observable<NewsPostDetailDto> {
    return this.http.post<NewsPostDetailDto>(this.baseUrl, request);
  }

  markAsRead(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/read`, {});
  }

  getStats(id: string): Observable<NewsReadStatsDto> {
    return this.http.get<NewsReadStatsDto>(`${this.baseUrl}/${id}/stats`);
  }

  getUnreadUsers(id: string): Observable<UnreadUserDto[]> {
    return this.http.get<UnreadUserDto[]>(`${this.baseUrl}/${id}/unread`);
  }
}
