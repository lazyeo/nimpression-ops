import {
  Component,
  ChangeDetectionStrategy,
  OnInit,
  signal,
  inject,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../../core/i18n/locale-date.pipe';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { AuthService } from '../../../core/auth/auth.service';
import { NewsService } from './services/news.service';
import {
  NewsAudience,
  NewsListFilter,
  NewsPostListItemDto,
  PagedResult,
} from './models/news.models';
import { NewsCreateDialogComponent } from './components/news-create-dialog/news-create-dialog.component';
import { NewsDetailModalComponent } from './components/news-detail-modal/news-detail-modal.component';

@Component({
  selector: 'nim-news',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    I18nPipe,
    LocaleDatePipe,
    IconComponent,
    NewsCreateDialogComponent,
    NewsDetailModalComponent,
  ],
  templateUrl: './news.component.html',
  styleUrls: ['./news.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewsComponent implements OnInit {
  readonly authService = inject(AuthService);
  private readonly newsService = inject(NewsService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly isForbidden = signal(false);
  readonly newsList = signal<NewsPostListItemDto[]>([]);
  readonly totalCount = signal(0);
  readonly totalPages = signal(1);
  readonly currentPage = signal(1);
  readonly pageSize = signal(20);

  // Filters
  readonly selectedAudience = signal<number | null>(null);
  readonly selectedPinned = signal<boolean | null>(null);
  readonly selectedActive = signal<boolean>(true);

  // Modals state
  readonly showCreateDialog = signal(false);
  readonly selectedNewsIdForDetail = signal<string | null>(null);

  // Stable sorted news: pinned first, then publishedAt descending
  readonly sortedNews = computed(() => {
    const list = [...this.newsList()];
    return list.sort((a, b) => {
      if (a.pinned !== b.pinned) {
        return a.pinned ? -1 : 1;
      }
      return new Date(b.publishedAt).getTime() - new Date(a.publishedAt).getTime();
    });
  });

  ngOnInit(): void {
    this.loadNews();
  }

  loadNews(): void {
    this.loading.set(true);
    this.error.set(null);
    this.isForbidden.set(false);

    const filter: NewsListFilter = {
      audience: this.selectedAudience(),
      isPinned: this.selectedPinned(),
      isActive: this.selectedActive(),
      page: this.currentPage(),
      pageSize: this.pageSize(),
    };

    this.newsService.getNews(filter).subscribe({
      next: (res: PagedResult<NewsPostListItemDto>) => {
        this.newsList.set(res.items || []);
        this.totalCount.set(res.totalCount || 0);
        this.totalPages.set(res.totalPages || 1);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        if (err.status === 403) {
          this.isForbidden.set(true);
        } else {
          this.error.set(err.message || 'NEWS.LOAD_FAILED');
        }
      },
    });
  }

  onAudienceFilterChange(val: string): void {
    const num = val === '' ? null : Number(val);
    this.selectedAudience.set(num);
    this.currentPage.set(1);
    this.loadNews();
  }

  onPinnedFilterChange(val: string): void {
    const pinned = val === '' ? null : val === 'true';
    this.selectedPinned.set(pinned);
    this.currentPage.set(1);
    this.loadNews();
  }

  onActiveFilterChange(active: boolean): void {
    this.selectedActive.set(active);
    this.currentPage.set(1);
    this.loadNews();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) return;
    this.currentPage.set(page);
    this.loadNews();
  }

  openCreateDialog(): void {
    this.showCreateDialog.set(true);
  }

  handleCreateClose(created: boolean): void {
    this.showCreateDialog.set(false);
    if (created) {
      this.currentPage.set(1);
      this.loadNews();
    }
  }

  openDetailModal(id: string): void {
    this.selectedNewsIdForDetail.set(id);
  }

  closeDetailModal(): void {
    this.selectedNewsIdForDetail.set(null);
  }

  markAsRead(item: NewsPostListItemDto, event: MouseEvent): void {
    event.stopPropagation();
    this.newsService.markAsRead(item.id).subscribe({
      next: () => {
        this.newsList.update((items) =>
          items.map((i) => (i.id === item.id ? { ...i, isRead: true, readAt: new Date().toISOString() } : i)),
        );
      },
    });
  }
}
