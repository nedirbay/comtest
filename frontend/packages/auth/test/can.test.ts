import { createApi } from '@comtest/api-client'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { describe, expect, it } from 'vitest'
import { defineComponent, h, nextTick, withDirectives } from 'vue'
import { vCan } from '../src/can'
import { API_KEY } from '../src/store'

const expiresAt = new Date(Date.now() + 900_000).toISOString()
const token = (permission: string[]) => `h.${btoa(JSON.stringify({ sub: 'u1', permission }))}.s`

function render(permission: string | string[], held: string[] | null) {
  const api = createApi({ baseUrl: 'https://api.test', fetch: (() => {}) as never })

  if (held) {
    api.session.accept({ accessToken: token(held), expiresAt })
  }

  const wrapper = mount(
    defineComponent({
      render: () => withDirectives(h('button', 'Delete'), [[vCan, permission]]),
    }),
    { global: { plugins: [createPinia()], provide: { [API_KEY as symbol]: api } } },
  )

  return { wrapper, api }
}

describe('v-can', () => {
  it('shows the element to a user who holds the permission', () => {
    const { wrapper } = render('catalog.write', ['catalog.write'])

    expect(wrapper.element.style.display).toBe('')
  })

  it('hides it from a user who does not', () => {
    const { wrapper } = render('catalog.write', ['catalog.read'])

    expect(wrapper.element.style.display).toBe('none')
  })

  it('hides it from a guest', () => {
    const { wrapper } = render('catalog.write', null)

    expect(wrapper.element.style.display).toBe('none')
  })

  it('accepts any one of several permissions', () => {
    const { wrapper } = render(['staff.manage', 'catalog.read'], ['catalog.read'])

    expect(wrapper.element.style.display).toBe('')
  })

  it('reacts when the token changes, for example after switching store', async () => {
    const { wrapper, api } = render('catalog.write', ['catalog.read'])

    api.session.accept({ accessToken: token(['catalog.write']), expiresAt })
    await nextTick()

    expect(wrapper.element.style.display).toBe('')
  })
})
