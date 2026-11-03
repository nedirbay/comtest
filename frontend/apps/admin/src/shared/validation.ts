import { z } from '@comtest/ui/zod'

export const SLUG_PATTERN = /^[a-z0-9]+(-[a-z0-9]+)*$/

/** A decimal typed by a person. It stays a string in the form, and the API receives its number. */
export const AMOUNT_PATTERN = /^\d{1,10}(\.\d{1,2})?$/

export const requiredText = (max = 300) =>
  z.string().trim().min(1, 'validation.required').max(max, 'validation.tooLong')

export const optionalText = (max = 1000) => z.string().trim().max(max, 'validation.tooLong')

export const slugField = z
  .string()
  .trim()
  .min(1, 'validation.required')
  .max(200, 'validation.tooLong')
  .regex(SLUG_PATTERN, 'admin.validation.slug')

export const requiredAmount = z
  .string()
  .trim()
  .min(1, 'validation.required')
  .regex(AMOUNT_PATTERN, 'admin.validation.amount')

export const optionalAmount = z
  .string()
  .trim()
  .refine((value) => value === '' || AMOUNT_PATTERN.test(value), 'admin.validation.amount')

export const optionalUrl = z
  .string()
  .trim()
  .max(500, 'validation.tooLong')
  .refine((value) => {
    if (!value) {
      return true
    }

    try {
      return ['http:', 'https:'].includes(new URL(value).protocol)
    } catch {
      return false
    }
  }, 'admin.validation.url')

/** For an optional number the API wants as null when absent. No arithmetic happens here. */
export function toAmount(value: string): number | null {
  return value.trim() === '' ? null : Number(value)
}

/** Empty text means "nothing" to the API, not an empty string. */
export function nullIfEmpty(value: string): string | null {
  const trimmed = value.trim()

  return trimmed === '' ? null : trimmed
}

/** Turns generated slugs and hand-typed names into the form the API accepts. */
export function slugify(value: string): string {
  const map: Record<string, string> = {
    ä: 'a',
    ç: 'c',
    ž: 'z',
    ň: 'n',
    ö: 'o',
    ş: 's',
    ü: 'u',
    ý: 'y',
    а: 'a',
    б: 'b',
    в: 'v',
    г: 'g',
    д: 'd',
    е: 'e',
    ё: 'e',
    ж: 'zh',
    з: 'z',
    и: 'i',
    й: 'y',
    к: 'k',
    л: 'l',
    м: 'm',
    н: 'n',
    о: 'o',
    п: 'p',
    р: 'r',
    с: 's',
    т: 't',
    у: 'u',
    ф: 'f',
    х: 'h',
    ц: 'c',
    ч: 'ch',
    ш: 'sh',
    щ: 'sch',
    ы: 'y',
    э: 'e',
    ю: 'yu',
    я: 'ya',
  }

  return value
    .toLowerCase()
    .split('')
    .map((char) => map[char] ?? char)
    .join('')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 200)
}
