import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { NewsService } from './news.service';
import { CreateNewsPostRequest, NewsAudience } from '../models/news.models';

describe('NewsService', () => {
  let service: NewsService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [NewsService, provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(NewsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('fetches news list with query parameters', () => {
    service
      .getNews({ audience: NewsAudience.Drivers, isPinned: true, page: 2, pageSize: 10 })
      .subscribe((res) => {
        expect(res.items.length).toBe(1);
        expect(res.totalCount).toBe(1);
      });

    const req = httpMock.expectOne((r) => r.url === '/api/news');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('audience')).toBe('2');
    expect(req.request.params.get('isPinned')).toBe('true');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('10');

    req.flush({
      items: [
        {
          id: 'n-1',
          title: 'Speed limit advisory',
          audience: NewsAudience.Drivers,
          publishedAt: '2026-09-01T10:00:00Z',
          pinned: true,
          isActive: true,
          isRead: false,
        },
      ],
      totalCount: 1,
      page: 2,
      pageSize: 10,
      totalPages: 1,
    });
  });

  it('creates bilingual news announcement', () => {
    const postReq: CreateNewsPostRequest = {
      title: 'Safety Policy Update',
      bodyEn: 'Please wear high-vis vests at all times.',
      bodyZh: 'In Chinese',
      audience: NewsAudience.All,
      pinned: true,
    };

    service.createNews(postReq).subscribe((res) => {
      expect(res.id).toBe('n-new');
      expect(res.title).toBe('Safety Policy Update');
    });

    const req = httpMock.expectOne('/api/news');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(postReq);

    req.flush({
      id: 'n-new',
      authorUserId: 'u-1',
      authorDisplayName: 'Admin User',
      title: 'Safety Policy Update',
      bodyEn: postReq.bodyEn,
      bodyZh: postReq.bodyZh,
      audience: postReq.audience,
      publishedAt: '2026-09-03T12:00:00Z',
      pinned: true,
      isActive: true,
      isRead: false,
    });
  });

  it('fetches read stats and unread users', () => {
    service.getStats('n-1').subscribe((stats) => {
      expect(stats.readCount).toBe(7);
      expect(stats.targetAudienceCount).toBe(10);
      expect(stats.readRate).toBe(0.7);
    });

    const statsReq = httpMock.expectOne('/api/news/n-1/stats');
    statsReq.flush({
      newsPostId: 'n-1',
      readCount: 7,
      targetAudienceCount: 10,
      readRate: 0.7,
    });

    service.getUnreadUsers('n-1').subscribe((users) => {
      expect(users.length).toBe(1);
      expect(users[0].displayName).toBe('John Doe');
    });

    const unreadReq = httpMock.expectOne('/api/news/n-1/unread');
    unreadReq.flush([
      {
        userId: 'u-2',
        displayName: 'John Doe',
        email: 'john@example.com',
        role: 3,
        employeeNo: 'DRV001',
      },
    ]);
  });
});
