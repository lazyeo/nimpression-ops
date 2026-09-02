import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { catchError, forkJoin, map, Observable, of, tap } from 'rxjs';
import { SupportedLang } from '../models/i18n.models';

const STORAGE_KEY = 'nim_locale_pref';

@Injectable({
  providedIn: 'root',
})
export class I18nService {
  private readonly http = inject(HttpClient, { optional: true });

  readonly currentLang = signal<SupportedLang>(this.resolveInitialLang());
  private readonly translationsMap = new Map<SupportedLang, Record<string, string>>();
  readonly isLoaded = signal<boolean>(false);

  constructor() {
    this.applyDocumentLang(this.currentLang());
    void this.loadLanguage(this.currentLang());
  }

  private resolveInitialLang(): SupportedLang {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored === 'zh-CN' || stored === 'en-NZ') {
        return stored;
      }
    } catch {
      // Ignore localStorage access issues
    }

    if (typeof navigator !== 'undefined') {
      const browserLang = navigator.language || '';
      if (browserLang.toLowerCase().startsWith('zh')) {
        return 'zh-CN';
      }
    }

    return 'en-NZ';
  }

  private applyDocumentLang(lang: SupportedLang): void {
    if (typeof document !== 'undefined') {
      document.documentElement.lang = lang;
    }
  }

  init(): Observable<boolean> {
    return forkJoin([this.loadLanguage('en-NZ'), this.loadLanguage('zh-CN')]).pipe(
      map(() => true),
      catchError(() => of(true)),
    );
  }

  setLanguage(lang: SupportedLang): Observable<boolean> {
    this.currentLang.set(lang);
    this.applyDocumentLang(lang);
    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      // Ignore storage error
    }

    return this.loadLanguage(lang);
  }

  loadLanguage(lang: SupportedLang): Observable<boolean> {
    if (this.translationsMap.has(lang)) {
      this.isLoaded.set(true);
      return of(true);
    }

    const url = `assets/i18n/${lang}.json`;

    if (this.http) {
      return this.http.get<Record<string, unknown>>(url).pipe(
        map((json) => {
          const flat = this.flattenKeys(json);
          this.translationsMap.set(lang, flat);
          this.isLoaded.set(true);
          return true;
        }),
        catchError(() => {
          this.isLoaded.set(true);
          return of(false);
        }),
      );
    }

    // Fallback for SSR or non-HTTP environments
    return of(true).pipe(
      tap(() => {
        this.isLoaded.set(true);
      }),
    );
  }

  setDictionary(lang: SupportedLang, dict: Record<string, unknown>): void {
    const flat = this.flattenKeys(dict);
    this.translationsMap.set(lang, flat);
    this.isLoaded.set(true);
  }

  translate(key: string, params?: Record<string, string | number>): string {
    const lang = this.currentLang();
    const dict = this.translationsMap.get(lang);
    let text = dict ? dict[key] : undefined;

    // Fallback to English dictionary if missing in target lang
    if (!text && lang !== 'en-NZ') {
      const enDict = this.translationsMap.get('en-NZ');
      text = enDict ? enDict[key] : undefined;
    }

    if (!text) {
      return key;
    }

    if (params) {
      for (const [paramKey, paramValue] of Object.entries(params)) {
        text = text.replace(new RegExp(`\\{${paramKey}\\}`, 'g'), String(paramValue));
      }
    }

    return text;
  }

  t(key: string, params?: Record<string, string | number>): string {
    return this.translate(key, params);
  }

  private flattenKeys(obj: Record<string, unknown>, prefix = ''): Record<string, string> {
    const result: Record<string, string> = {};
    for (const [k, v] of Object.entries(obj)) {
      const fullKey = prefix ? `${prefix}.${k}` : k;
      if (v && typeof v === 'object' && !Array.isArray(v)) {
        Object.assign(result, this.flattenKeys(v as Record<string, unknown>, fullKey));
      } else if (typeof v === 'string') {
        result[fullKey] = v;
      }
    }
    return result;
  }
}
