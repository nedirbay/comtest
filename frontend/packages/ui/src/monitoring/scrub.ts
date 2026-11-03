/** Fields that must never leave the browser in an error report, in any casing or position. */
const SECRET_KEY =
  /pass(word)?|token|secret|authorization|cookie|otp|code|recovery|email|phone|fullname|address/i
const JWT = /\beyJ[\w-]+\.[\w-]+\.[\w-]+/g
const EMAIL = /[\w.+-]+@[\w-]+\.[\w.-]+/g
const PHONE = /\+?\d[\d\s()-]{7,}\d/g

/** Machine-made identifiers. They can look like phone numbers, and reports break without them. */
const TECHNICAL_KEY =
  /^(event_id|trace_id|span_id|parent_span_id|release|dist|timestamp|start_timestamp)$/

export const REDACTED = '[redacted]'

function scrubString(value: string): string {
  return value.replace(JWT, REDACTED).replace(EMAIL, REDACTED).replace(PHONE, REDACTED)
}

/** Secrets in email links travel in the fragment, and others may sit in the query string. */
export function scrubUrl(url: string): string {
  const [withoutFragment] = url.split('#')
  const [path] = (withoutFragment ?? '').split('?')

  return scrubString(path ?? '')
}

/** Walks a report and replaces whatever looks personal. Depth is limited so cycles cannot hang it. */
export function scrubValue(value: unknown, depth = 0): unknown {
  if (typeof value === 'string') {
    return scrubString(value)
  }

  if (typeof value !== 'object' || value === null || depth > 8) {
    return value
  }

  if (Array.isArray(value)) {
    return value.map((item) => scrubValue(item, depth + 1))
  }

  return Object.fromEntries(
    Object.entries(value).map(([key, item]) => {
      if (TECHNICAL_KEY.test(key)) {
        return [key, item]
      }

      return [key, SECRET_KEY.test(key) ? REDACTED : scrubValue(item, depth + 1)]
    }),
  )
}

interface ScrubbableEvent {
  user?: unknown
  request?: {
    url?: string
    headers?: unknown
    cookies?: unknown
    data?: unknown
    query_string?: unknown
  }
  extra?: unknown
  contexts?: unknown
  breadcrumbs?: unknown
  message?: string
  exception?: unknown
}

/** For Sentry's `beforeSend`. The user's identity and request content are removed entirely. */
export function scrubEvent<T extends ScrubbableEvent>(event: T): T {
  const clean = scrubValue(event) as T

  delete clean.user

  if (clean.request) {
    delete clean.request.headers
    delete clean.request.cookies
    delete clean.request.data
    delete clean.request.query_string

    if (event.request?.url) {
      clean.request.url = scrubUrl(event.request.url)
    }
  }

  return clean
}

interface ScrubbableBreadcrumb {
  data?: Record<string, unknown>
}

function scrubBreadcrumb<T extends ScrubbableBreadcrumb>(breadcrumb: T): T {
  const url = breadcrumb.data?.url

  if (typeof url === 'string') {
    breadcrumb.data = { ...breadcrumb.data, url: scrubUrl(url) }
  }

  return breadcrumb
}

/**
 * Spread into `Sentry.init`. The SDK collects users, cookies, headers, request bodies and query
 * strings unless told otherwise, so each of them is switched off here, and the callbacks clean up
 * anything that still slips into an error message.
 */
export const sentryPrivacyOptions = {
  dataCollection: {
    userInfo: false,
    cookies: false,
    httpHeaders: false,
    httpBodies: [],
    urlQueryParams: false,
    stackFrameVariables: false,
  },
  beforeSend: scrubEvent,
  beforeSendTransaction: scrubEvent,
  beforeBreadcrumb: scrubBreadcrumb,
}
