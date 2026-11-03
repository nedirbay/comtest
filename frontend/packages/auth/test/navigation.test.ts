import { describe, expect, it } from 'vitest'
import { safeReturnUrl } from '../src/navigation'

describe('safeReturnUrl', () => {
  it.each([
    ['/orders', '/orders'],
    ['/orders/1?tab=items#notes', '/orders/1?tab=items#notes'],
    ['/ru/account', '/ru/account'],
  ])('keeps the internal path %s', (input, expected) => {
    expect(safeReturnUrl(input)).toBe(expected)
  })

  it.each([
    'https://evil.tm',
    '//evil.tm',
    '///evil.tm',
    '/\\evil.tm',
    '\\\\evil.tm',
    'javascript:alert(1)',
    ' /orders',
    '/\t/evil.tm',
    '/\n/evil.tm',
    'evil.tm',
    '',
    null,
    undefined,
    42,
    ['/orders'],
  ])('rejects %j', (input) => {
    expect(safeReturnUrl(input, '/home')).toBe('/home')
  })
})
