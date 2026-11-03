import en from 'element-plus/es/locale/lang/en'
import ru from 'element-plus/es/locale/lang/ru'
import { tk } from './tk'

export const SUPPORTED_LOCALES = ['tk', 'ru', 'en'] as const
export type AppLocale = (typeof SUPPORTED_LOCALES)[number]
export const DEFAULT_LOCALE: AppLocale = 'tk'

export const LOCALE_NAMES: Record<AppLocale, string> = {
  tk: 'Türkmençe',
  ru: 'Русский',
  en: 'English',
}

export const elementPlusLocales = { tk, ru, en }

export function isAppLocale(value: unknown): value is AppLocale {
  return typeof value === 'string' && (SUPPORTED_LOCALES as readonly string[]).includes(value)
}
