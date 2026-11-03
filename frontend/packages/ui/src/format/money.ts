import { resolveFormatLocale } from './locale'

export interface MoneyValue {
  amount: number
  currency: string
}

const SYMBOLS: Record<string, string> = { TMT: 'TMT', USD: '$' }

/**
 * Formats an amount the server calculated. This module never adds or multiplies money, because
 * totals come from the API.
 */
export function formatMoney(value: MoneyValue, locale: string): string {
  const number = new Intl.NumberFormat(resolveFormatLocale(locale), {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value.amount)
  const symbol = SYMBOLS[value.currency] ?? value.currency

  return value.currency === 'USD' ? `${symbol}${number}` : `${number}\u00a0${symbol}`
}
