import { describe, expect, it } from 'vitest'
import { formatDate, formatDateTime, formatMoney, formatPhone, normalizePhone } from '../src/format'

describe('formatMoney', () => {
  it('uses a space for thousands and a comma for decimals in Turkmen and Russian', () => {
    expect(formatMoney({ amount: 1250.5, currency: 'TMT' }, 'tk')).toBe('1 250,50 TMT')
    expect(formatMoney({ amount: 1250.5, currency: 'TMT' }, 'ru')).toBe('1 250,50 TMT')
  })

  it('puts the dollar sign first', () => {
    expect(formatMoney({ amount: 19.9, currency: 'USD' }, 'en')).toBe('$19.90')
  })
})

describe('formatDate', () => {
  const noon = '2026-11-03T07:00:00Z'

  it('spells Turkmen months itself, as runtimes often have no data for them', () => {
    expect(formatDate(noon, 'tk')).toBe('3 noýabr 2026')
  })

  it('applies the Ashgabat time zone', () => {
    expect(formatDate('2026-11-03T20:00:00Z', 'tk')).toBe('4 noýabr 2026')
    expect(formatDateTime(noon, 'tk')).toBe('3 noýabr 2026, 12:00')
  })

  it('formats other languages through Intl', () => {
    expect(formatDate(noon, 'en')).toBe('3 November 2026')
    expect(formatDate(noon, 'ru')).toContain('ноября')
  })
})

describe('phone numbers', () => {
  it('groups Turkmen numbers for reading', () => {
    expect(formatPhone('+99365123456')).toBe('+993 65 123456')
    expect(formatPhone('+15551234567')).toBe('+15551234567')
  })

  it('normalizes input to E.164', () => {
    expect(normalizePhone('+993 65 12-34-56')).toBe('+99365123456')
    expect(normalizePhone('65123456')).toBe('+99365123456')
    expect(normalizePhone('123')).toBeNull()
  })
})
