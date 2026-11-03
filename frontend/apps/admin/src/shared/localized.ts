import { SUPPORTED_LOCALES, type AppLocale } from '@comtest/ui/locale'

interface HasLanguage {
  language: string
  name: string
}

/** The name in the language on screen, then any other. An item is never left without a label. */
export function localizedName(translations: readonly HasLanguage[], locale: string): string {
  return translations.find((item) => item.language === locale)?.name ?? translations[0]?.name ?? ''
}

export const LANGUAGES: readonly AppLocale[] = SUPPORTED_LOCALES
