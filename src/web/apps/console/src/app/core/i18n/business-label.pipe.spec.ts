import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { I18nService } from './i18n.service';
import { BusinessLabelPipe } from './business-label.pipe';
import enDictionary from '../../../assets/i18n/en-NZ.json';
import zhDictionary from '../../../assets/i18n/zh-CN.json';

describe('BusinessLabelPipe', () => {
  let pipe: BusinessLabelPipe;
  const language = signal('en-NZ');
  let loaded = true;

  beforeEach(() => {
    loaded = true;
    language.set('en-NZ');
    TestBed.configureTestingModule({
      providers: [
        {
          provide: I18nService,
          useValue: {
            currentLang: language,
            isLoaded: signal(true),
            translate: (key: string, params?: Record<string, number>) => {
              if (!loaded) return key;
              const dictionary: unknown = language() === 'zh-CN' ? zhDictionary : enDictionary;
              const value = key
                .split('.')
                .reduce<unknown>(
                  (current, part) =>
                    current && typeof current === 'object' && Object.hasOwn(current, part)
                      ? (current as Record<string, unknown>)[part]
                      : undefined,
                  dictionary,
                );
              return typeof value === 'string'
                ? value.replace('{index}', String(params?.['index']))
                : key;
            },
          },
        },
      ],
    });
    pipe = TestBed.runInInjectionContext(() => new BusinessLabelPipe());
  });

  it('labels recorded reading sources in both languages and reacts to switching', () => {
    expect(pipe.transform('DriverApp', 'readingSource')).toBe('Driver submission');
    expect(pipe.transform('AdminConsole', 'readingSource')).toBe('Office entry');
    language.set('zh-CN');
    expect(pipe.transform('DriverApp', 'readingSource')).toBe('司机提交');
    expect(pipe.transform('AdminConsole', 'readingSource')).toBe('后台录入');
  });

  it('uses business labels for statuses, pay lines, templates and audit actions', () => {
    expect(pipe.transform('InProgress', 'taskStatus')).toBe('In Progress');
    expect(pipe.transform('DeadLetter', 'emailStatus')).toBe('Delivery stopped');
    expect(pipe.transform('MinimumWageTopUp', 'payLine')).toBe('Minimum wage adjustment');
    expect(pipe.transform('SERVICE_DUE_REMINDER', 'template')).toBe('Vehicle service reminder');
    expect(pipe.transform('AssignDriverToArea', 'auditAction')).toBe('Assign driver to area');
  });

  it('does not expose unknown enum values, property paths or inherited object keys', () => {
    expect(pipe.transform('FutureStatus', 'taskStatus')).toBe('Not available');
    expect(pipe.transform('some.private.path', 'auditField', 2)).toBe('Other field 3');
    expect(pipe.transform('constructor', 'template')).toBe('Not available');
    expect(pipe.transform('Driver', '__proto__')).toBe('Not available');
    expect(pipe.transform('FutureRole', 'actorRole')).toBe('Not available');
  });

  it('keeps cold-load fallbacks readable without exposing translation keys', () => {
    loaded = false;
    expect(pipe.transform('DriverApp', 'readingSource')).toBe('Not available');
    language.set('zh-CN');
    expect(pipe.transform('DriverApp', 'readingSource')).toBe('暂无信息');
    expect(pipe.transform('unknown', 'auditField', 1)).toBe('其他字段 2');
  });
});
