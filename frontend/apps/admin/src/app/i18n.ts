import { authMessages } from '@comtest/auth/i18n'
import { writePreference, readPreference } from '@comtest/ui'
import { uiMessages } from '@comtest/ui/i18n'
import { DEFAULT_LOCALE, isAppLocale, SUPPORTED_LOCALES, type AppLocale } from '@comtest/ui/locale'
import { createI18n } from 'vue-i18n'
import { adminMessages } from './messages'

const messages = Object.fromEntries(
  SUPPORTED_LOCALES.map((locale) => [
    locale,
    { ...uiMessages[locale], ...authMessages[locale], ...adminMessages[locale] },
  ]),
) as Record<
  AppLocale,
  (typeof adminMessages)['tk'] & (typeof uiMessages)['tk'] & (typeof authMessages)['tk']
>

export const i18n = createI18n({
  legacy: false,
  locale: DEFAULT_LOCALE,
  fallbackLocale: 'en',
  messages,
})

const LOCALE_PREFERENCE = 'locale'

/** The language for an address that does not name one: the saved choice, then the browser's. */
export function preferredLocale(): AppLocale {
  const saved = readPreference(LOCALE_PREFERENCE)

  if (isAppLocale(saved)) {
    return saved
  }

  const browser = navigator.language.split('-')[0]

  return isAppLocale(browser) ? browser : DEFAULT_LOCALE
}

export function applyLocale(locale: AppLocale): void {
  i18n.global.locale.value = locale
  document.documentElement.lang = locale
  writePreference(LOCALE_PREFERENCE, locale)
}
