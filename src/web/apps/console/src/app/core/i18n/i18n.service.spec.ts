import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { I18nService } from './i18n.service';

describe('I18nService', () => {
  let service: I18nService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [I18nService, provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
    service = TestBed.inject(I18nService);

    // Consume initial constructor load request if pending
    const req = httpMock.match('assets/i18n/en-NZ.json');
    if (req.length > 0) {
      req[0].flush({
        COMMON: { OK: 'OK', GREETING: 'Hello, {name}' },
        AUTH: { LOGIN: 'Sign in' },
      });
    }

    // Provide test dictionaries
    service.setDictionary('en-NZ', {
      COMMON: { OK: 'OK', GREETING: 'Hello, {name}' },
      AUTH: { LOGIN: 'Sign in' },
    });
    service.setDictionary('zh-CN', {
      COMMON: { OK: '\u786e\u5b9a', GREETING: '\u4f60\u597d\uff0c{name}' },
      AUTH: { LOGIN: '\u767b\u5f55' },
    });
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('translates keys in current language and handles parameters', () => {
    service.setLanguage('en-NZ');
    expect(service.translate('COMMON.OK')).toBe('OK');
    expect(service.translate('COMMON.GREETING', { name: 'John' })).toBe('Hello, John');

    service.setLanguage('zh-CN');
    expect(service.translate('COMMON.OK')).toBe('\u786e\u5b9a');
    expect(service.translate('COMMON.GREETING', { name: '\u5c0f\u660e' })).toBe(
      '\u4f60\u597d\uff0c\u5c0f\u660e',
    );
  });

  it('falls back to English if key missing in target language', () => {
    service.setDictionary('zh-CN', { COMMON: {} });
    service.setLanguage('zh-CN');
    expect(service.translate('AUTH.LOGIN')).toBe('Sign in');
  });

  it('uses readable localized fallback for missing keys while preserving ordinary text', () => {
    service.setLanguage('en-NZ');
    expect(service.translate('NON.EXISTENT.KEY')).toBe('Information unavailable');
    expect(service.translate('VEHICLES.SECTION_ASSIGNMENT')).toBe('Information unavailable');
    service.setLanguage('zh-CN');
    expect(service.translate('NON.EXISTENT.KEY')).toBe('信息暂不可用');
    expect(service.translate('Log out')).toBe('Log out');
    expect(service.translate('已取消')).toBe('已取消');
    expect(service.translate('TASK-001')).toBe('TASK-001');
  });

  it('renders an available assignment label in both languages', () => {
    service.setDictionary('en-NZ', { VEHICLES: { SECTION_ASSIGNMENT: 'Assignment' } });
    service.setDictionary('zh-CN', { VEHICLES: { SECTION_ASSIGNMENT: '分配信息' } });
    service.setLanguage('en-NZ');
    expect(service.translate('VEHICLES.SECTION_ASSIGNMENT')).toBe('Assignment');
    service.setLanguage('zh-CN');
    expect(service.translate('VEHICLES.SECTION_ASSIGNMENT')).toBe('分配信息');
  });

  it('persists language preference and updates document element lang', () => {
    service.setLanguage('zh-CN');
    expect(document.documentElement.lang).toBe('zh-CN');

    service.setLanguage('en-NZ');
    expect(document.documentElement.lang).toBe('en-NZ');
  });
});
