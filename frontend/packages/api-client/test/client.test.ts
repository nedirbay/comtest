import { describe, expect, it, vi } from 'vitest'
import { createApi, unwrap, ApiError } from '../src'

const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })

const grant = {
  accessToken: 'h.e30.renewed',
  expiresAt: new Date(Date.now() + 900_000).toISOString(),
}

function setup(respond: (request: Request) => Response) {
  const requests: Request[] = []
  const fetch = vi.fn(async (input: Request) => {
    requests.push(input.clone())

    return respond(input)
  }) as unknown as typeof globalThis.fetch

  return {
    requests,
    api: createApi({
      baseUrl: 'https://api.test/',
      fetch,
      getLocale: () => 'ru',
      getHeaders: () => ({ 'X-Tenant': 'demo' }),
    }),
  }
}

describe('createApi', () => {
  it('adds the language, tenant, correlation and CSRF headers', async () => {
    const { api, requests } = setup(() => json(200, {}))

    await api.client.GET('/api/v1/store')

    const headers = requests[0]!.headers
    expect(headers.get('Accept-Language')).toBe('ru')
    expect(headers.get('X-Tenant')).toBe('demo')
    expect(headers.get('X-Requested-With')).toBe('ComTest')
    expect(headers.get('X-Correlation-ID')).toMatch(/^[0-9a-f]{32}$/)
    expect(requests[0]!.credentials).toBe('include')
  })

  it('sends no Authorization header for a guest', async () => {
    const { api, requests } = setup(() => json(200, {}))

    await api.client.GET('/api/v1/store')

    expect(requests[0]!.headers.has('Authorization')).toBe(false)
  })

  it('never sends the access token to the sign-in endpoints', async () => {
    const { api, requests } = setup(() => json(200, {}))

    api.session.accept(grant)
    await api.client.POST('/api/v1/auth/logout')

    expect(requests[0]!.headers.has('Authorization')).toBe(false)
  })

  it('renews the token once on 401 and repeats the request with its body', async () => {
    let calls = 0
    const { api, requests } = setup((request) => {
      if (request.url.endsWith('/auth/refresh')) {
        return json(200, grant)
      }

      calls += 1

      return calls === 1 ? json(401, { code: 'common.unauthorized' }) : json(200, { ok: true })
    })

    api.session.accept({ ...grant, accessToken: 'h.e30.old' })

    const { response } = await api.client.POST('/api/v1/account/password', {
      body: { currentPassword: 'a', newPassword: 'b' },
    })

    expect(response.status).toBe(200)

    const repeated = requests.at(-1)!
    expect(repeated.headers.get('Authorization')).toBe('Bearer h.e30.renewed')
    expect(await repeated.json()).toEqual({ currentPassword: 'a', newPassword: 'b' })
  })

  it('does not loop when the renewed token is rejected too', async () => {
    const { api, requests } = setup((request) =>
      request.url.endsWith('/auth/refresh')
        ? json(200, grant)
        : json(401, { code: 'common.unauthorized' }),
    )

    api.session.accept({ ...grant, accessToken: 'h.e30.old' })

    const { response } = await api.client.GET('/api/v1/account/me')

    expect(response.status).toBe(401)
    expect(requests.map((request) => new URL(request.url).pathname)).toEqual([
      '/api/v1/account/me',
      '/api/v1/auth/refresh',
      '/api/v1/account/me',
    ])
  })

  it('turns a problem response into an ApiError with field errors', async () => {
    const { api } = setup(() =>
      json(400, {
        title: 'Maglumat nädogry',
        code: 'common.validation',
        correlationId: 'abc',
        errors: { email: ['Nädogry e-poçta'] },
      }),
    )

    const error = await unwrap(
      api.client.POST('/api/v1/auth/login', { body: { login: 'x', password: 'y' } }),
    ).catch((caught: unknown) => caught as ApiError)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({
      status: 400,
      code: 'common.validation',
      message: 'Maglumat nädogry',
      correlationId: 'abc',
      fieldErrors: { email: ['Nädogry e-poçta'] },
    })
  })

  it('reports a failed connection as a network error', async () => {
    const api = createApi({
      baseUrl: 'https://api.test',
      fetch: vi.fn().mockRejectedValue(new TypeError('failed')) as typeof globalThis.fetch,
    })

    const error = await unwrap(api.client.GET('/api/v1/store')).catch(
      (caught: unknown) => caught as ApiError,
    )

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).isNetworkError).toBe(true)
  })
})
