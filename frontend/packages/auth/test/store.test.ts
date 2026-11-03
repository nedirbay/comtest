import { createApi, type Api } from '@comtest/api-client'
import { readPreference, writePreference } from '@comtest/ui'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createApp } from 'vue'
import { API_KEY, useAuthStore } from '../src/store'

const accessToken = (claims: object) => `h.${btoa(JSON.stringify(claims))}.s`
const expiresAt = new Date(Date.now() + 900_000).toISOString()
const profile = { id: 'u1', email: 'a@b.tm', fullName: 'A', roles: [] }

function json(status: number, body: unknown = {}) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function setup(respond: (path: string) => Response) {
  const calls: string[] = []
  const api: Api = createApi({
    baseUrl: 'https://api.test',
    fetch: (async (input: Request) => {
      const path = new URL(input.url).pathname

      calls.push(path)

      return respond(path)
    }) as typeof globalThis.fetch,
  })
  const app = createApp({})
  const pinia = createPinia()

  app.provide(API_KEY, api)
  app.use(pinia)
  setActivePinia(pinia)

  return { api, calls, auth: useAuthStore() }
}

const signedInResponse = {
  twoFactorRequired: false,
  challengeToken: null,
  accessToken: accessToken({
    sub: 'u1',
    permission: ['catalog.read'],
    role: ['Manager'],
    tid: 't1',
  }),
  expiresAt,
  twoFactorSetupRequired: false,
}

describe('useAuthStore', () => {
  beforeEach(() => writePreference('session', null))

  it('treats a visitor without a session hint as a guest and calls nothing', async () => {
    const { auth, calls } = setup(() => json(500))

    await auth.bootstrap()

    expect(auth.status).toBe('guest')
    expect(calls).toEqual([])
  })

  it('restores the session from the cookie when the hint is present', async () => {
    writePreference('session', '1')

    const { auth, calls } = setup((path) =>
      path.endsWith('/refresh') ? json(200, signedInResponse) : json(200, profile),
    )

    await auth.bootstrap()

    expect(auth.isAuthenticated).toBe(true)
    expect(auth.profile).toMatchObject({ email: 'a@b.tm' })
    expect(calls).toEqual(['/api/v1/auth/refresh', '/api/v1/account/me'])
  })

  it('drops a stale hint when the refresh cookie is no longer valid', async () => {
    writePreference('session', '1')

    const { auth } = setup(() => json(401, { code: 'auth.refresh_invalid' }))

    await auth.bootstrap()

    expect(auth.status).toBe('guest')
    expect(readPreference('session')).toBeNull()
  })

  it('stays signed out but keeps the hint when the server cannot be reached', async () => {
    writePreference('session', '1')

    const { auth } = setup(() => {
      throw new TypeError('offline')
    })

    await auth.bootstrap()

    expect(auth.status).toBe('guest')
    expect(readPreference('session')).toBe('1')
  })

  it('stores no token in browser storage after signing in', async () => {
    const { auth } = setup((path) =>
      path.endsWith('/login') ? json(200, signedInResponse) : json(200, profile),
    )

    await auth.login('a@b.tm', 'password')

    expect(auth.isAuthenticated).toBe(true)
    expect(JSON.stringify({ ...globalThis.localStorage })).not.toContain(
      signedInResponse.accessToken,
    )
    expect(JSON.stringify({ ...globalThis.sessionStorage })).not.toContain(
      signedInResponse.accessToken,
    )
  })

  it('reports the second-factor stage without signing in', async () => {
    const { auth } = setup(() =>
      json(200, {
        twoFactorRequired: true,
        challengeToken: 'challenge',
        twoFactorSetupRequired: false,
      }),
    )

    expect(await auth.login('a@b.tm', 'password')).toEqual({
      kind: 'two-factor',
      challengeToken: 'challenge',
    })
    expect(auth.isAuthenticated).toBe(false)
  })

  it('clears everything on logout and tells subscribers, even when the server fails', async () => {
    const { auth, api } = setup((path) => {
      if (path.endsWith('/logout')) {
        return json(500)
      }

      return path.endsWith('/login') ? json(200, signedInResponse) : json(200, profile)
    })
    const onSignedOut = vi.fn()

    auth.onSignedOut(onSignedOut)
    await auth.login('a@b.tm', 'password')
    await auth.logout()

    expect(api.session.isAuthenticated).toBe(false)
    expect(auth.profile).toBeNull()
    expect(auth.claims).toBeNull()
    expect(readPreference('session')).toBeNull()
    expect(onSignedOut).toHaveBeenCalledOnce()
  })

  it('answers permission checks from the token', async () => {
    const { auth } = setup((path) =>
      path.endsWith('/login') ? json(200, signedInResponse) : json(200, profile),
    )

    expect(auth.can('catalog.read')).toBe(false)

    await auth.login('a@b.tm', 'password')

    expect(auth.can('catalog.read')).toBe(true)
    expect(auth.can('staff.manage')).toBe(false)
    expect(auth.can(['staff.manage', 'catalog.read'])).toBe(true)
    expect(auth.hasActiveTenant).toBe(true)
  })
})
