import { describe, expect, it } from 'vitest'
import { safeUrl } from '../src/security/safeUrl'
import { sanitizeHtml } from '../src/security/sanitize'

describe('safeUrl', () => {
  it.each([
    'https://kargo.tm/track/1',
    'http://kargo.tm',
    'mailto:shop@example.tm',
    'tel:+99365123456',
    '/relative/path',
    'relative/path',
  ])('keeps %s', (url) => {
    expect(safeUrl(url)).toBe(url)
  })

  it.each([
    'javascript:alert(1)',
    'JaVaScRiPt:alert(1)',
    ' javascript:alert(1)',
    'java\nscript:alert(1)',
    'java\tscript:alert(1)',
    'data:text/html,<script>alert(1)</script>',
    'vbscript:msgbox(1)',
    'file:///etc/passwd',
  ])('refuses %j', (url) => {
    expect(safeUrl(url)).toBeUndefined()
  })

  it('treats nothing as nothing', () => {
    expect(safeUrl(null)).toBeUndefined()
    expect(safeUrl('')).toBeUndefined()
  })
})

describe('sanitizeHtml', () => {
  it('keeps the formatting the editor produces', () => {
    const html = '<h2>Sözbaşy</h2><p><strong>Gowy</strong> <em>haryt</em></p><ul><li>bir</li></ul>'

    expect(sanitizeHtml(html)).toBe(html)
  })

  it.each([
    ['script', '<script>alert(1)</script><p>ok</p>'],
    ['event handler', '<img src="x" onerror="alert(1)"><p>ok</p>'],
    ['javascript link', '<a href="javascript:alert(1)">ok</a>'],
    ['svg', '<svg onload="alert(1)"></svg><p>ok</p>'],
    ['iframe', '<iframe src="https://evil.tm"></iframe><p>ok</p>'],
    ['style', '<p style="position:fixed">ok</p><style>body{display:none}</style>'],
    ['form', '<form action="https://evil.tm"><input name="password"></form><p>ok</p>'],
  ])('removes a dangerous %s', (_name, html) => {
    const clean = sanitizeHtml(html)

    expect(clean).not.toMatch(
      /alert|onerror|onload|javascript:|<iframe|<style|style=|<form|<input|<svg|<script/i,
    )
  })

  it('stops links opening a new tab from reaching back to this page', () => {
    const clean = sanitizeHtml('<a href="https://kargo.tm" target="_blank">ok</a>')

    expect(clean).toContain('rel="noopener noreferrer"')
  })

  it('returns nothing for nothing', () => {
    expect(sanitizeHtml(null)).toBe('')
  })
})
