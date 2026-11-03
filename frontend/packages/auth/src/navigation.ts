const HOST_PROBE = 'https://internal.invalid'

/**
 * Accepts a return address only if it stays on this site. Without this check a link like
 * /login?returnUrl=https://evil.tm sends people to a copy of the site right after they sign in.
 */
export function safeReturnUrl(value: unknown, fallback = '/'): string {
  if (typeof value !== 'string' || !value.startsWith('/') || value.startsWith('//')) {
    return fallback
  }

  // Browsers read a backslash as a slash, and they strip control characters before parsing.
  // eslint-disable-next-line no-control-regex
  if (/[\\\u0000-\u001f]/.test(value)) {
    return fallback
  }

  try {
    const url = new URL(value, HOST_PROBE)

    return url.origin === HOST_PROBE ? `${url.pathname}${url.search}${url.hash}` : fallback
  } catch {
    return fallback
  }
}

/**
 * Reads a link's secret from the URL fragment and removes it from the address bar, so that it stays
 * out of the browser history. Fragments are never sent to servers or in Referer headers.
 */
export function takeFragmentParams(): URLSearchParams {
  const params = new URLSearchParams(window.location.hash.replace(/^#/, ''))

  if (window.location.hash) {
    window.history.replaceState(
      window.history.state,
      '',
      window.location.pathname + window.location.search,
    )
  }

  return params
}
