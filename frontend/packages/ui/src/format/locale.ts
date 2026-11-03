/**
 * Browsers and Node disagree about Turkmen: many have no data for `tk` and silently fall back to
 * the system language, which would differ between the server's HTML and the browser's. Numbers
 * therefore follow the Russian pattern that Turkmen also uses (1 250,50), and dates are built here.
 */
export function resolveFormatLocale(locale: string): string {
  return locale === 'en' ? 'en-GB' : 'ru-RU'
}
