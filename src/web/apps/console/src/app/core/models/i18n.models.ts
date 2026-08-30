export type SupportedLang = 'en-NZ' | 'zh-CN';

export interface LanguageOption {
  code: SupportedLang;
  labelKey: string;
}

export const SUPPORTED_LANGUAGES: LanguageOption[] = [
  { code: 'en-NZ', labelKey: 'LANG.EN_NZ' },
  { code: 'zh-CN', labelKey: 'LANG.ZH_CN' },
];
