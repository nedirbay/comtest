import { createApi } from '@comtest/api-client'
import { API_KEY } from '@comtest/auth'
import { createPinia, setActivePinia } from 'pinia'
import { describe, expect, it } from 'vitest'
import { createApp, defineComponent, h } from 'vue'
import {
  maskEmail,
  maskName,
  maskPhone,
  useCustomerPrivacy,
} from '../src/shared/composables/useCustomerPrivacy'

const token = (claims: object) => `h.${btoa(JSON.stringify(claims))}.s`

function privacyFor(permissions: string[]) {
  const pinia = createPinia()
  const app = createApp(defineComponent({ render: () => h('div') }))
  const api = createApi({
    baseUrl: 'https://api.test',
    fetch: (async () => new Response('{}')) as typeof globalThis.fetch,
  })

  api.session.accept({
    accessToken: token({ sub: 'u1', permission: permissions, tid: 't1', mfa: true }),
    expiresAt: new Date(Date.now() + 900_000).toISOString(),
  })
  app.provide(API_KEY, api)
  app.use(pinia)
  setActivePinia(pinia)

  return app.runWithContext(() => useCustomerPrivacy())
}

describe('customer details', () => {
  it('leaves little of a name, phone or email', () => {
    expect(maskName('Aýna Myratowa')).toBe('A•••')
    expect(maskPhone('+99361234567')).toBe('••• 67')
    expect(maskEmail('ayna@example.com')).toBe('a•••@example.com')
  })

  it('shows everything to someone with customers.read', () => {
    const privacy = privacyFor(['orders.read', 'customers.read'])

    expect(privacy.canSee.value).toBe(true)
    expect(privacy.name('Aýna Myratowa')).toBe('Aýna Myratowa')
    expect(privacy.phone('+99361234567')).toBe('+99361234567')
    expect(privacy.address('Magtymguly 1')).toBe('Magtymguly 1')
  })

  it('hides it from someone who only has orders.read', () => {
    const privacy = privacyFor(['orders.read'])

    expect(privacy.canSee.value).toBe(false)
    expect(privacy.name('Aýna Myratowa')).not.toContain('Myratowa')
    expect(privacy.phone('+99361234567')).not.toContain('612345')
    expect(privacy.email('ayna@example.com')).not.toContain('ayna')
    expect(privacy.address('Magtymguly 1')).not.toContain('Magtymguly')
  })
})
