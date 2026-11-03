import { generateHTML, generateJSON } from '@tiptap/vue-3'
import { describe, expect, it } from 'vitest'
import {
  editorExtensions,
  hasUnsupportedContent,
} from '../src/features/catalog/components/editorExtensions'

/** The tags in `packages/ui/src/security/sanitize.ts`, which is also what the API keeps. */
const ALLOWED = new Set([
  'p',
  'br',
  'strong',
  'b',
  'em',
  'i',
  'u',
  'ul',
  'ol',
  'li',
  'h2',
  'h3',
  'h4',
  'blockquote',
  'table',
  'thead',
  'tbody',
  'tr',
  'th',
  'td',
  'a',
  'img',
  'span',
])

function roundTrip(html: string): string {
  const extensions = editorExtensions()

  return generateHTML(generateJSON(html, extensions), extensions)
}

function tagsIn(html: string): string[] {
  return [...html.matchAll(/<([a-z0-9]+)/gi)].map((match) => (match[1] as string).toLowerCase())
}

describe('the rich text editor', () => {
  it('produces only tags the sanitizer allows, whatever goes in', () => {
    const html = roundTrip(`
      <h1>Bir</h1><h2>Iki</h2><h5>Bäş</h5>
      <p style="color:red" onclick="x()">Tekst <s>üstü</s> <code>kod</code> <u>asty</u></p>
      <pre><code>kod</code></pre><hr>
      <ul><li>Bir</li></ul><ol><li>Iki</li></ol><blockquote><p>Sitata</p></blockquote>
      <script>alert(1)</script><iframe src="https://evil.tm"></iframe>
    `)

    expect(tagsIn(html).filter((tag) => !ALLOWED.has(tag))).toEqual([])
    expect(html).not.toMatch(/onclick|style=|alert|iframe/)
    expect(html).toContain('Tekst')
  })

  it('keeps what a description is made of', () => {
    const html = roundTrip(
      '<h2>Sözbaşy</h2><p><strong>Galyň</strong> <em>ýapgyt</em> <u>çyzykly</u></p>',
    )

    expect(html).toContain('<h2>Sözbaşy</h2>')
    expect(html).toContain('<strong>Galyň</strong>')
    expect(html).toContain('<em>ýapgyt</em>')
    expect(html).toContain('<u>çyzykly</u>')
  })

  it('drops a link that could run code, and keeps a real one safe', () => {
    expect(roundTrip('<p><a href="javascript:alert(1)">x</a></p>')).not.toContain('javascript:')

    const safe = roundTrip('<p><a href="https://example.com">x</a></p>')

    expect(safe).toContain('href="https://example.com"')
    expect(safe).toContain('noopener')
  })

  it('warns about content it would lose on the first edit', () => {
    expect(hasUnsupportedContent('<p>Tekst</p>')).toBe(false)
    expect(hasUnsupportedContent('<table><tr><td>1</td></tr></table>')).toBe(true)
    expect(hasUnsupportedContent('<p><img src="a.png"></p>')).toBe(true)
  })
})
