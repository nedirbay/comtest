import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'
import { adminMessages } from '../src/app/messages'

const SOURCE = join(import.meta.dirname, '../src')

function sourceFiles(folder: string): string[] {
  return readdirSync(folder).flatMap((name) => {
    const path = join(folder, name)

    if (statSync(path).isDirectory()) {
      return sourceFiles(path)
    }

    return /\.(vue|ts)$/.test(name) && name !== 'messages.ts' ? [path] : []
  })
}

function flatten(value: object, prefix = ''): string[] {
  return Object.entries(value).flatMap(([key, item]) =>
    typeof item === 'object' && item !== null
      ? flatten(item as object, `${prefix}${key}.`)
      : [`${prefix}${key}`],
  )
}

const usedKeys = [
  ...new Set(
    sourceFiles(SOURCE).flatMap((file) =>
      [...readFileSync(file, 'utf8').matchAll(/['"`](admin\.[A-Za-z_.]+[A-Za-z_])['"`]/g)].map(
        (match) => match[1] as string,
      ),
    ),
  ),
]

describe('admin messages', () => {
  const known = new Set(flatten(adminMessages.tk))

  it('has a text for every key the code names', () => {
    expect(usedKeys.length).toBeGreaterThan(100)
    expect(usedKeys.filter((key) => !known.has(key))).toEqual([])
  })

  it.each(['ru', 'en'] as const)('has the same keys in %s', (locale) => {
    expect(flatten(adminMessages[locale]).sort()).toEqual([...known].sort())
  })

  // These characters are syntax to vue-i18n and would break the message at run time.
  it('uses no characters the translator would read as syntax', () => {
    for (const locale of ['tk', 'ru', 'en'] as const) {
      const texts = JSON.stringify(adminMessages[locale])

      expect(texts).not.toMatch(/[@|]/)
    }
  })

  it('covers every value the API can send for what is built from a template', () => {
    const texts = adminMessages.tk.admin as unknown as Record<string, Record<string, string>>

    for (const type of [
      'receipt',
      'adjustment',
      'reservation',
      'reservationRelease',
      'sale',
      'return',
    ]) {
      expect(texts.movements?.[`type_${type}`]).toBeTruthy()
    }

    for (const type of [
      'created',
      'confirmed',
      'cancelled',
      'refundRequested',
      'refundCompleted',
      'refundRejected',
    ]) {
      expect(texts.order?.[`event_${type}`]).toBeTruthy()
    }

    for (const status of ['confirmed', 'packed', 'shipped', 'delivered', 'cancelled', 'returned']) {
      expect(texts.order?.[`action_${status}`]).toBeTruthy()
    }

    for (const type of ['text', 'number', 'boolean', 'select']) {
      expect(texts.attributes?.[`type_${type}`]).toBeTruthy()
    }
  })
})
