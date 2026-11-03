import { createApi } from '@comtest/api-client'
import { API_KEY } from '@comtest/auth'
import { createPinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createApp, defineComponent, h } from 'vue'
import { createAppRouter } from '../src/app/router'

vi.stubGlobal(
  'matchMedia',
  vi.fn(() => ({ matches: false })),
)

const expiresAt = new Date(Date.now() + 900_000).toISOString()
const token = (claims: object) => `h.${btoa(JSON.stringify(claims))}.s`

function setup(claims: object | null, profile: object = { twoFactorEnabled: true }) {
  const pinia = createPinia()
  const app = createApp(defineComponent({ render: () => h('div') }))
  const api = createApi({
    baseUrl: 'https://api.test',
    fetch: (async () =>
      new Response(JSON.stringify(profile), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })) as typeof globalThis.fetch,
  })

  if (claims) {
    api.session.accept({ accessToken: token(claims), expiresAt })
  }

  app.provide(API_KEY, api)
  app.use(pinia)

  const router = createAppRouter(pinia)

  app.use(router)

  return { router, api }
}

const owner = {
  sub: 'u1',
  role: ['StoreOwner'],
  permission: ['catalog.read'],
  tid: 't1',
  mfa: true,
}

describe('admin route guards', () => {
  beforeEach(() => window.history.replaceState({}, '', '/'))

  it('sends a guest to sign-in and remembers where they were going', async () => {
    const { router } = setup(null)

    await router.push('/ru/security')

    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query.returnUrl).toBe('/ru/security')
    expect(router.currentRoute.value.params.locale).toBe('ru')
  })

  it('gives an address without a language the preferred one', async () => {
    const { router } = setup(null)

    await router.push('/security')

    expect(router.currentRoute.value.path).toMatch(/^\/(tk|ru|en)\/login$/)
    expect(router.currentRoute.value.query.returnUrl).toMatch(/^\/(tk|ru|en)\/security$/)
  })

  it('does not let a signed-in user stay on the sign-in page', async () => {
    const { router } = setup(owner)

    await router.push('/tk/login')

    expect(router.currentRoute.value.name).toBe('dashboard')
  })

  it('ignores an external return address after sign-in', async () => {
    const { router } = setup(owner)

    await router.push('/tk/login?returnUrl=https://evil.tm')

    expect(router.currentRoute.value.path).toBe('/tk')
  })

  it('sends staff without a chosen store to the store list', async () => {
    const { router } = setup({ ...owner, tid: undefined, role: [] })

    await router.push('/tk')

    expect(router.currentRoute.value.name).toBe('select-store')
  })

  it('turns away a user without the permission a page asks for', async () => {
    const { router } = setup({ ...owner, permission: ['catalog.read'] })

    await router.push('/tk/settings')
    expect(router.currentRoute.value.name).toBe('forbidden')

    await router.push('/tk/products/new')
    expect(router.currentRoute.value.name).toBe('forbidden')
  })

  it('names a permission on every page of the panel except the ones for everyone', () => {
    const { router } = setup(owner)
    const open = new Set(['dashboard', 'security', 'forbidden'])
    const pages = router
      .getRoutes()
      .filter((route) => route.meta.layout === 'admin' && !open.has(String(route.name)))

    expect(pages.length).toBeGreaterThan(15)
    expect(pages.filter((route) => !route.meta.permission).map((route) => route.name)).toEqual([])
  })

  it('shows public error pages to guests', async () => {
    const { router } = setup(null)

    await router.push('/tk/no/such/page')

    expect(router.currentRoute.value.name).toBe('not-found')
  })
})
