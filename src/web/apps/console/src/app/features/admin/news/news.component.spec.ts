import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { NewsComponent } from './news.component';
import { NewsService } from './services/news.service';
import { AuthService } from '../../../core/auth/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { NewsAudience, NewsPostListItemDto, PagedResult } from './models/news.models';

describe('NewsComponent (Admin News & Notices)', () => {
  let component: NewsComponent;
  let fixture: ComponentFixture<NewsComponent>;
  let httpMock: HttpTestingController;

  const mockNewsItems: NewsPostListItemDto[] = [
    {
      id: 'news-1',
      title: 'Older Pinned Notice',
      audience: NewsAudience.All,
      publishedAt: '2026-08-01T08:00:00Z',
      pinned: true,
      isActive: true,
      isRead: true,
      readAt: '2026-08-01T09:00:00Z',
    },
    {
      id: 'news-2',
      title: 'Newer Regular Notice',
      audience: NewsAudience.Drivers,
      publishedAt: '2026-09-01T08:00:00Z',
      pinned: false,
      isActive: true,
      isRead: false,
    },
  ];

  beforeEach(async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [NewsComponent],
      providers: [
        NewsService,
        AuthService,
        I18nService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(NewsComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('renders list with stable sorting (pinned first, then publishedAt desc)', () => {
    fixture.detectChanges();

    const req = httpMock.expectOne((r) => r.url === '/api/news');
    expect(req.request.method).toBe('GET');

    const mockResponse: PagedResult<NewsPostListItemDto> = {
      items: mockNewsItems,
      totalCount: 2,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    };
    req.flush(mockResponse);
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.newsList().length).toBe(2);

    // Stable sort check: pinned item first despite older publishedAt
    const sorted = component.sortedNews();
    expect(sorted[0].id).toBe('news-1');
    expect(sorted[0].pinned).toBe(true);
    expect(sorted[1].id).toBe('news-2');
    expect(sorted[1].pinned).toBe(false);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.data-table')).toBeTruthy();
    expect(compiled.querySelectorAll('tbody tr').length).toBe(2);
  });

  it('renders empty data state when list is empty', () => {
    fixture.detectChanges();

    const req = httpMock.expectOne((r) => r.url === '/api/news');
    req.flush({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.sortedNews().length).toBe(0);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.empty-state')).toBeTruthy();
  });

  it('handles audience filter and pagination transitions', () => {
    fixture.detectChanges();

    const initialReq = httpMock.expectOne((r) => r.url === '/api/news');
    initialReq.flush({
      items: mockNewsItems,
      totalCount: 25,
      page: 1,
      pageSize: 20,
      totalPages: 2,
    });
    fixture.detectChanges();

    // Change audience filter to Drivers (2)
    component.onAudienceFilterChange('2');

    const filterReq = httpMock.expectOne(
      (r) => r.url === '/api/news' && r.params.get('audience') === '2',
    );
    filterReq.flush({
      items: [mockNewsItems[1]],
      totalCount: 25,
      page: 1,
      pageSize: 20,
      totalPages: 2,
    });
    fixture.detectChanges();

    expect(component.selectedAudience()).toBe(2);
    expect(component.newsList().length).toBe(1);

    // Change page to 2
    component.goToPage(2);
    const pageReq = httpMock.expectOne(
      (r) => r.url === '/api/news' && r.params.get('page') === '2',
    );
    pageReq.flush({
      items: [],
      totalCount: 25,
      page: 2,
      pageSize: 20,
      totalPages: 2,
    });
    fixture.detectChanges();

    expect(component.currentPage()).toBe(2);
  });

  it('renders error state with retry functionality on API failure', () => {
    fixture.detectChanges();

    const req = httpMock.expectOne((r) => r.url === '/api/news');
    req.flush('Internal Server Error', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.error()).toBeTruthy();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.error-state')).toBeTruthy();

    // Trigger retry
    component.loadNews();
    const retryReq = httpMock.expectOne((r) => r.url === '/api/news');
    retryReq.flush({
      items: mockNewsItems,
      totalCount: 2,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
    fixture.detectChanges();

    expect(component.error()).toBeNull();
    expect(component.newsList().length).toBe(2);
  });

  it('renders forbidden state on 403 response', () => {
    fixture.detectChanges();

    const req = httpMock.expectOne((r) => r.url === '/api/news');
    req.flush('Forbidden', { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();

    expect(component.isForbidden()).toBe(true);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.forbidden-state')).toBeTruthy();
  });
});
