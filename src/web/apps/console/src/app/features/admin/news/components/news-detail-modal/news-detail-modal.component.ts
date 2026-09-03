import {
  Component,
  ChangeDetectionStrategy,
  input,
  output,
  signal,
  inject,
  OnInit,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { I18nPipe } from '../../../../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../../../../core/i18n/locale-date.pipe';
import { IconComponent } from '../../../../../shared/components/icon/icon.component';
import { NewsService } from '../../services/news.service';
import {
  NewsPostDetailDto,
  NewsReadStatsDto,
  UnreadUserDto,
} from '../../models/news.models';

@Component({
  selector: 'nim-news-detail-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, I18nPipe, LocaleDatePipe, IconComponent],
  templateUrl: './news-detail-modal.component.html',
  styleUrls: ['./news-detail-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewsDetailModalComponent implements OnInit {
  private readonly newsService = inject(NewsService);

  readonly newsId = input.required<string>();
  readonly closeModal = output<void>();

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly newsPost = signal<NewsPostDetailDto | null>(null);
  readonly stats = signal<NewsReadStatsDto | null>(null);
  readonly unreadUsers = signal<UnreadUserDto[]>([]);
  readonly activeLangTab = signal<'en' | 'zh'>('en');
  readonly unreadSearchTerm = signal('');

  readonly filteredUnreadUsers = computed(() => {
    const term = this.unreadSearchTerm().toLowerCase().trim();
    const list = this.unreadUsers();
    if (!term) return list;
    return list.filter(
      (u) =>
        u.displayName.toLowerCase().includes(term) ||
        u.email.toLowerCase().includes(term) ||
        (u.employeeNo && u.employeeNo.toLowerCase().includes(term)),
    );
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.error.set(null);
    const id = this.newsId();

    this.newsService.getNewsById(id).subscribe({
      next: (post) => {
        this.newsPost.set(post);
        this.loadStatsAndUnread(id);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.message || 'NEWS.LOAD_DETAIL_FAILED');
      },
    });
  }

  private loadStatsAndUnread(id: string): void {
    this.newsService.getStats(id).subscribe({
      next: (statsData) => {
        this.stats.set(statsData);
      },
      error: () => {
        // Continue even if stats fail
      },
    });

    this.newsService.getUnreadUsers(id).subscribe({
      next: (unreadData) => {
        this.unreadUsers.set(unreadData);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  setLangTab(tab: 'en' | 'zh'): void {
    this.activeLangTab.set(tab);
  }

  close(): void {
    this.closeModal.emit();
  }
}
