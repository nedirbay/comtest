import { createApi } from '@comtest/api-client'
import { uiMessages } from '@comtest/ui/i18n'
import { flushPromises, mount } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import { createPinia } from 'pinia'
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import { createI18n } from 'vue-i18n'
import LoginForm from '../src/components/LoginForm.vue'
import { authMessages } from '../src/i18n'
import { API_KEY } from '../src/store'

const API = 'https://api.test'
const token = `h.${btoa(JSON.stringify({ sub: 'u1', permission: [] }))}.s`

const server = setupServer()

beforeAll(() => server.listen({ onUnhandledFrame: 'error' }))
afterEach(() => server.resetHandlers())
afterAll(() => server.close())

function setup() {
  const i18n = createI18n({
    legacy: false,
    locale: 'tk',
    messages: { tk: { ...uiMessages.tk, ...authMessages.tk } },
  })
  const api = createApi({ baseUrl: API })
  const inputStub = {
    props: ['modelValue'],
    emits: ['update:modelValue'],
    template:
      '<input :name="$attrs.name" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />',
  }

  const wrapper = mount(LoginForm, {
    global: {
      plugins: [createPinia(), i18n],
      provide: { [API_KEY as symbol]: api },
      stubs: {
        'el-form': {
          template: '<form @submit.prevent="$emit(\'submit\', $event)"><slot /></form>',
        },
        'el-form-item': {
          props: ['error'],
          template: '<div><slot /><em v-if="error">{{ error }}</em></div>',
        },
        'el-input': inputStub,
        'el-button': {
          props: ['nativeType'],
          template: '<button :type="nativeType"><slot /></button>',
        },
        'el-alert': { props: ['title'], template: '<p role="alert">{{ title }}</p>' },
      },
    },
  })

  return { wrapper, api }
}

async function submit(wrapper: ReturnType<typeof setup>['wrapper']): Promise<void> {
  await wrapper.find('input[name="username"]').setValue('ayna@example.tm')
  await wrapper.find('input[name="password"]').setValue('a-password')
  await wrapper.find('form').trigger('submit')
  await vi.waitFor(() => flushPromises())
}

const profile = { id: 'u1', email: 'ayna@example.tm', fullName: 'Aýna', roles: [] }

describe('LoginForm', () => {
  it('signs in, keeps the token out of the page and forgets the password', async () => {
    server.use(
      http.post(`${API}/api/v1/auth/login`, () =>
        HttpResponse.json({
          twoFactorRequired: false,
          challengeToken: null,
          accessToken: token,
          expiresAt: new Date(Date.now() + 900_000).toISOString(),
          twoFactorSetupRequired: false,
        }),
      ),
      http.get(`${API}/api/v1/account/me`, () => HttpResponse.json(profile)),
    )

    const { wrapper, api } = setup()

    await submit(wrapper)
    await vi.waitFor(() => expect(wrapper.emitted('signed-in')).toHaveLength(1))

    expect(api.session.isAuthenticated).toBe(true)
    expect(wrapper.html()).not.toContain(token)
    expect((wrapper.find('input[name="password"]').element as HTMLInputElement).value).toBe('')
  })

  it("shows the server's message for a wrong password", async () => {
    server.use(
      http.post(`${API}/api/v1/auth/login`, () =>
        HttpResponse.json(
          { title: 'Login ýa-da açar sözi nädogry', code: 'auth.invalid_credentials' },
          { status: 401 },
        ),
      ),
    )

    const { wrapper } = setup()

    await submit(wrapper)
    await vi.waitFor(() =>
      expect(wrapper.find('[role="alert"]').text()).toBe('Login ýa-da açar sözi nädogry'),
    )
    expect(wrapper.emitted('signed-in')).toBeUndefined()
  })

  it('moves to the second factor when the account has one', async () => {
    server.use(
      http.post(`${API}/api/v1/auth/login`, () =>
        HttpResponse.json({
          twoFactorRequired: true,
          challengeToken: 'challenge',
          accessToken: null,
          expiresAt: null,
          twoFactorSetupRequired: false,
        }),
      ),
    )

    const { wrapper } = setup()

    await submit(wrapper)
    await vi.waitFor(() => expect(wrapper.emitted('stage-change')?.[0]).toEqual(['two-factor']))
    expect(wrapper.emitted('signed-in')).toBeUndefined()
  })

  it('does not call the server when a field is empty', async () => {
    const called = vi.fn()

    server.use(http.post(`${API}/api/v1/auth/login`, called))

    const { wrapper } = setup()

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(called).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Bu meýdany dolduryň')
  })

  it('offers to resend the confirmation for an unconfirmed email', async () => {
    server.use(
      http.post(`${API}/api/v1/auth/login`, () =>
        HttpResponse.json(
          { title: 'E-poçta tassyklanmadyk', code: 'auth.email_not_confirmed' },
          { status: 403 },
        ),
      ),
    )

    const { wrapper } = setup()

    await submit(wrapper)
    await vi.waitFor(() => expect(wrapper.text()).toContain('Tassyklama hatyny täzeden ibermek'))
  })
})
