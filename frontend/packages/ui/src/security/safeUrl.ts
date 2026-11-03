const SAFE_PROTOCOLS = new Set(['http:', 'https:', 'mailto:', 'tel:'])
const HOST_PROBE = 'https://internal.invalid'

/**
 * Returns a link from outside (a tracking address, a logo) only if it cannot run code. Vue escapes
 * text but not the meaning of an href, so `javascript:` would still fire when clicked.
 */
export function safeUrl(value: string | null | undefined): string | undefined {
  if (!value) {
    return undefined
  }

  try {
    // Browsers strip control characters and whitespace before parsing, so `java\nscript:` counts.
    return SAFE_PROTOCOLS.has(new URL(value.trim(), HOST_PROBE).protocol) ? value.trim() : undefined
  } catch {
    return undefined
  }
}
