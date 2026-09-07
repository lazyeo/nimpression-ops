import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { LocaleDatePipe } from './locale-date.pipe';
import { FormatService } from './format.service';
import { I18nService } from './i18n.service';

describe('LocaleDatePipe', () => {
  let pipe: LocaleDatePipe;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [LocaleDatePipe, FormatService, I18nService, provideHttpClient(), provideHttpClientTesting()],
    });
    pipe = TestBed.inject(LocaleDatePipe);
  });

  it('formats short, medium, and datetime presets', () => {
    const date = new Date(2026, 8, 7, 17, 59, 0);

    const shortVal = pipe.transform(date, 'short', 'en-NZ');
    expect(shortVal).toContain('2026');

    const shortDateTimeVal = pipe.transform(date, 'shortDateTime', 'en-NZ');
    expect(shortDateTimeVal).toContain('2026');
    expect(shortDateTimeVal).toContain('59');

    const mediumDateTimeVal = pipe.transform(date, 'mediumDateTime', 'en-NZ');
    expect(mediumDateTimeVal).toContain('2026');
    expect(mediumDateTimeVal).toContain('59');
  });

  it('handles empty and null values safely', () => {
    expect(pipe.transform(null)).toBe('');
    expect(pipe.transform(undefined)).toBe('');
    expect(pipe.transform('')).toBe('');
  });
});
