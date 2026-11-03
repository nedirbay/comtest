import createDOMPurify, { type DOMPurify, type WindowLike } from 'dompurify'

/** The same list the API keeps when it saves a description, so that nothing shifts on display. */
const ALLOWED_TAGS = [
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
]
const ALLOWED_ATTR = ['href', 'src', 'alt', 'title', 'target', 'rel', 'colspan', 'rowspan']

let purifier: DOMPurify | null = null

function configure(instance: DOMPurify): DOMPurify {
  // A link that opens a new tab must not hand that tab a reference back to this page.
  instance.addHook('afterSanitizeAttributes', (node) => {
    if (node.tagName === 'A') {
      node.setAttribute('rel', 'noopener noreferrer')
    }
  })

  return instance
}

/** For server rendering, where there is no window. The server creates one with jsdom at startup. */
export function setSanitizerWindow(window: WindowLike): void {
  purifier = configure(createDOMPurify(window))
}

/**
 * Cleans HTML for `v-html`. The API sanitizes on saving too, but this is the last check before it
 * reaches the page. Without a working sanitizer it returns nothing, never the raw input.
 */
export function sanitizeHtml(html: string | null | undefined): string {
  if (!html) {
    return ''
  }

  if (!purifier && typeof window !== 'undefined') {
    purifier = configure(createDOMPurify(window))
  }

  return purifier?.sanitize(html, { ALLOWED_TAGS, ALLOWED_ATTR, ALLOW_DATA_ATTR: false }) ?? ''
}
