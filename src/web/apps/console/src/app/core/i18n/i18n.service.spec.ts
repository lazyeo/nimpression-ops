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

  it('returns key if missing completely', () => {
    expect(service.translate('NON.EXISTENT.KEY')).toBe('NON.EXISTENT.KEY');
  });

  it('persists language preference and updates document element lang', () => {
    service.setLanguage('zh-CN');
    expect(document.documentElement.lang).toBe('zh-CN');

    service.setLanguage('en-NZ');
    expect(document.documentElement.lang).toBe('en-NZ');
  });
});
