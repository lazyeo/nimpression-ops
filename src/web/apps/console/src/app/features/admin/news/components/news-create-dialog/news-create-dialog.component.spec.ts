import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { NewsCreateDialogComponent } from './news-create-dialog.component';
import { NewsService } from '../../services/news.service';
import { I18nService } from '../../../../../core/i18n/i18n.service';
import { NewsAudience } from '../../models/news.models';

describe('NewsCreateDialogComponent', () => {
  let component: NewsCreateDialogComponent;
  let fixture: ComponentFixture<NewsCreateDialogComponent>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [NewsCreateDialogComponent],
      providers: [
        NewsService,
        I18nService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(NewsCreateDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('validates required fields including bilingual content (bodyEn and bodyZh)', () => {
    expect(component.form.valid).toBe(false);

    // Set only title and audience
    component.form.patchValue({
      title: 'Monthly Safety Briefing',
      audience: 'All',
      bodyEn: 'English body content',
      bodyZh: '', // Missing Chinese body
    });

    expect(component.form.valid).toBe(false);
    expect(component.form.controls.bodyZh.valid).toBe(false);

    // Provide Chinese body
    component.form.patchValue({
      bodyZh: 'Chinese body content',
    });

    expect(component.form.valid).toBe(true);
  });

  it('submits valid bilingual announcement and emits closeDialog on success', () => {
    let closedWithSuccess = false;
    component.closeDialog.subscribe((success) => {
      closedWithSuccess = success;
    });

    component.form.patchValue({
      title: 'Fleet Meeting',
      audience: 'Drivers',
      pinned: true,
      bodyEn: 'Meeting at 9am.',
      bodyZh: 'Meeting text in Chinese.',
    });

    component.submitForm();

    const req = httpMock.expectOne('/api/news');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.title).toBe('Fleet Meeting');
    expect(req.request.body.bodyEn).toBe('Meeting at 9am.');
    expect(req.request.body.bodyZh).toBe('Meeting text in Chinese.');
    expect(req.request.body.audience).toBe('Drivers');
    expect(req.request.body.pinned).toBe(true);

    req.flush({
      id: 'news-created',
      authorUserId: 'u-1',
      authorDisplayName: 'Admin',
      title: 'Fleet Meeting',
      bodyEn: 'Meeting at 9am.',
      bodyZh: 'Meeting text in Chinese.',
      audience: 'Drivers',
      publishedAt: '2026-09-03T10:00:00Z',
      pinned: true,
      isActive: true,
      isRead: false,
    });

    expect(closedWithSuccess).toBe(true);
  });
});
