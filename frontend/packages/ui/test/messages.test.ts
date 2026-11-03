import { describe, expect, it } from 'vitest'
import { authMessages } from '../../auth/src/i18n'
import { adminMessages } from '../../../apps/admin/src/app/messages'
import { storeMessages } from '../../../apps/storefront/i18n/messages'
import { uiMessages } from '../src/i18n'
import { elementPlusLocales } from '../src/locale'

const LOCALES = ['tk', 'ru', 'en'] as const

/** Every leaf of a nested message object, as `a.b.c` with its text. */
function flatten(value: unknown, prefix = ''): Record<string, string> {
  if (typeof value === 'string') {
    return { [prefix]: value }
  }

  if (Array.isArray(value)) {
    return Object.assign({}, ...value.map((item, index) => flatten(item, `${prefix}[${index}]`)))
  }

  return Object.assign(
    {},
    ...Object.entries(value as object).map(([key, item]) =>
      flatten(item, prefix ? `${prefix}.${key}` : key),
    ),
  )
}

const placeholders = (text: string) =>
  [...text.matchAll(/\{(\w+)\}/g)].map((match) => match[1]).sort()

describe.each([
  ['ui', uiMessages],
  ['auth', authMessages],
  ['admin', adminMessages],
  ['storefront', storeMessages],
])('%s translations', (_name, messages) => {
  const flat = Object.fromEntries(LOCALES.map((locale) => [locale, flatten(messages[locale])]))

  it.each(['ru', 'en'] as const)('%s has the same keys as Turkmen', (locale) => {
    expect(Object.keys(flat[locale]!).sort()).toEqual(Object.keys(flat.tk!).sort())
  })

  it.each(LOCALES)('%s has no empty text', (locale) => {
    const empty = Object.entries(flat[locale]!).filter(([, text]) => text.trim() === '')

    expect(empty).toEqual([])
  })

  it.each(['ru', 'en'] as const)('%s uses the same placeholders as Turkmen', (locale) => {
    for (const [key, text] of Object.entries(flat.tk!)) {
      expect(placeholders(flat[locale]![key]!), key).toEqual(placeholders(text))
    }
  })
})

describe('Element Plus translations', () => {
  it('Turkmen covers everything the English file has', () => {
    const english = Object.keys(flatten(elementPlusLocales.en.el))
    const turkmen = new Set(Object.keys(flatten(elementPlusLocales.tk.el)))

    expect(english.filter((key) => !turkmen.has(key))).toEqual([])
  })
})
