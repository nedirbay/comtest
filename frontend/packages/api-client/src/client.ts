import createClient, { type Client } from 'openapi-fetch'
import { ApiError } from './errors'
import type { paths } from './generated/schema'
import { AuthSession, type AccessGrant, type AuthSessionOptions } from './session'

export type ApiClient = Client<paths>

export interface ApiOptions {
  /** Absolute origin of the API, without a trailing slash. */
  baseUrl: string
  /** The language the server should answer in. */
  getLocale?: () => string
  /** Headers added to every request, such as `X-Tenant` during development. */
  getHeaders?: () => Record<string, string>
  onSessionExpired?: () => void
  locks?: AuthSessionOptions['locks']
  fetch?: typeof globalThis.fetch
}

export interface Api {
  client: ApiClient
  session: AuthSession
}

const REFRESH_PATH = '/api/v1/auth/refresh'

/** Requests that carry a password or a challenge. Their 401 is an answer, not an expired token. */
const SIGN_IN_PATHS = /\/api\/v\d+\/auth\//

/** Tells the API this call comes from our own scripts. See the backend's CsrfProtection. */
const CSRF_HEADER = { 'X-Requested-With': 'ComTest' }

export function createApi(options: ApiOptions): Api {
  const baseFetch = options.fetch ?? globalThis.fetch.bind(globalThis)
  const baseUrl = options.baseUrl.replace(/\/+$/, '')

  // Cookies go to this API's origin and nowhere else, as nothing here takes a full URL from outside.
  const send = (request: Request): Promise<Response> =>
    baseFetch(request).catch((cause: unknown) => {
      if (cause instanceof DOMException && cause.name === 'AbortError') {
        throw cause
      }

      throw ApiError.network(cause)
    })

  const decorate = (request: Request): void => {
    const locale = options.getLocale?.()

    if (locale) {
      request.headers.set('Accept-Language', locale)
    }

    for (const [name, value] of Object.entries(options.getHeaders?.() ?? {})) {
      request.headers.set(name, value)
    }

    request.headers.set('X-Correlation-ID', createCorrelationId())

    for (const [name, value] of Object.entries(CSRF_HEADER)) {
      request.headers.set(name, value)
    }
  }

  const session = new AuthSession({
    locks: options.locks ?? null,
    ...(options.onSessionExpired ? { onExpired: options.onSessionExpired } : {}),
    refresh: async () => {
      const request = new Request(`${baseUrl}${REFRESH_PATH}`, {
        method: 'POST',
        credentials: 'include',
      })

      decorate(request)

      const response = await send(request)
      const body: unknown = await response.json().catch(() => undefined)

      if (!response.ok) {
        throw ApiError.fromResponse(response, body)
      }

      return body as AccessGrant
    },
  })

  const authorizedFetch = async (input: Request): Promise<Response> => {
    const isAuthEndpoint = SIGN_IN_PATHS.test(new URL(input.url).pathname)

    decorate(input)

    const token = isAuthEndpoint ? null : await session.getToken()
    const retry = token ? input.clone() : null

    if (token) {
      input.headers.set('Authorization', `Bearer ${token}`)
    }

    const response = await send(input)

    // The token may have been revoked or expired on the way. Renew once, and never in a loop.
    if (response.status === 401 && retry) {
      const renewed = await session.refresh()

      if (renewed) {
        retry.headers.set('Authorization', `Bearer ${renewed}`)

        return send(retry)
      }
    }

    return response
  }

  const client = createClient<paths>({
    baseUrl,
    credentials: 'include',
    fetch: authorizedFetch,
  })

  return { client, session }
}

/** Returns a successful call's data and throws {@link ApiError} for anything else. */
export async function unwrap<T>(
  call: Promise<{ data?: T; error?: unknown; response: Response }>,
): Promise<T> {
  const { data, error, response } = await call

  if (!response.ok) {
    throw ApiError.fromResponse(response, error)
  }

  return data as T
}

function createCorrelationId(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(16))

  return Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('')
}
