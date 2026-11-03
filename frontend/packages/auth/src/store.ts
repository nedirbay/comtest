import { ApiError, unwrap, type AccessClaims, type Api, type Schema } from '@comtest/api-client'
import { readPreference, writePreference } from '@comtest/ui'
import { defineStore } from 'pinia'
import { computed, inject, ref, shallowRef, type InjectionKey } from 'vue'

export const API_KEY: InjectionKey<Api> = Symbol('comtest-api')

/**
 * A hint that this browser may have a session, and not a credential. It only spares guests a
 * refresh request that would fail anyway on every page load.
 */
const SESSION_HINT = 'session'
const CHANNEL = 'comtest-auth'
const STAFF_ROLES = ['StoreOwner', 'Manager', 'Staff', 'PlatformAdmin']

type Status = 'unknown' | 'guest' | 'authenticated'
type ChannelMessage = 'signed-in' | 'signed-out'

export type LoginOutcome = { kind: 'signed-in' } | { kind: 'two-factor'; challengeToken: string }

export const useAuthStore = defineStore('auth', () => {
  const api = inject(API_KEY)

  if (!api) {
    throw new Error('Provide the API with app.provide(API_KEY, api) before using the auth store.')
  }

  const status = ref<Status>('unknown')
  const claims = shallowRef<AccessClaims | null>(null)
  const profile = shallowRef<Schema<'ProfileDto'> | null>(null)
  const setupRequiredAtLogin = ref(false)

  const signedOutHandlers = new Set<() => void>()
  let bootstrapping: Promise<void> | null = null
  let channel: BroadcastChannel | null = null
  let hadSession = false

  function syncFromSession(): void {
    claims.value = api!.session.claims
    status.value = api!.session.isAuthenticated ? 'authenticated' : 'guest'
    hadSession ||= api!.session.isAuthenticated
  }

  api.session.subscribe(syncFromSession)

  // The session can be signed in before anything first asks for this store.
  if (api.session.isAuthenticated) {
    syncFromSession()
  }

  function broadcast(message: ChannelMessage): void {
    channel?.postMessage(message)
  }

  /** Forgets everything about the user in this tab: token, profile and whatever subscribed. */
  function clearLocalState(): void {
    api!.session.clear()
    profile.value = null
    setupRequiredAtLogin.value = false
    writePreference(SESSION_HINT, null)

    // A failed restore on page load ended nothing the user could see, so nobody needs telling.
    if (hadSession) {
      hadSession = false

      for (const handler of signedOutHandlers) {
        handler()
      }
    }
  }

  function connectTabs(): void {
    if (channel || typeof BroadcastChannel === 'undefined') {
      return
    }

    channel = new BroadcastChannel(CHANNEL)
    channel.onmessage = ({ data }: MessageEvent<ChannelMessage>) => {
      if (data === 'signed-out') {
        clearLocalState()
      } else if (data === 'signed-in' && status.value !== 'authenticated') {
        bootstrapping = null
        void bootstrap()
      }
    }
  }

  async function loadProfile(): Promise<void> {
    profile.value = await unwrap(api!.client.GET('/api/v1/account/me'))
  }

  /** Called once per page load in the browser. It restores the session from the refresh cookie. */
  function bootstrap(): Promise<void> {
    bootstrapping ??= (async () => {
      connectTabs()

      if (api!.session.isAuthenticated) {
        if (!profile.value) {
          await loadProfile()
        }

        return
      }

      if (readPreference(SESSION_HINT) === null) {
        status.value = 'guest'

        return
      }

      try {
        const token = await api!.session.refresh()

        if (token) {
          await loadProfile()
        } else {
          clearLocalState()
        }
      } catch (error) {
        // The session may be fine while the network is not. Treat the user as a guest for now and
        // keep the hint, so that the next page load tries again.
        if (!(error instanceof ApiError)) {
          throw error
        }

        status.value = 'guest'
      }
    })()

    return bootstrapping
  }

  async function finishSignIn(response: Schema<'LoginResponse'>): Promise<void> {
    api!.session.accept({
      accessToken: response.accessToken as string,
      expiresAt: response.expiresAt as string,
    })
    setupRequiredAtLogin.value = response.twoFactorSetupRequired
    writePreference(SESSION_HINT, '1')
    await loadProfile()
    broadcast('signed-in')
  }

  async function login(loginName: string, password: string): Promise<LoginOutcome> {
    const response = await unwrap(
      api!.client.POST('/api/v1/auth/login', { body: { login: loginName, password } }),
    )

    if (response.twoFactorRequired) {
      return { kind: 'two-factor', challengeToken: response.challengeToken as string }
    }

    await finishSignIn(response)

    return { kind: 'signed-in' }
  }

  async function completeTwoFactor(
    challengeToken: string,
    secondFactor: { code: string } | { recoveryCode: string },
  ): Promise<void> {
    await finishSignIn(
      await unwrap(
        api!.client.POST('/api/v1/auth/login/two-factor', {
          body: { challengeToken, ...secondFactor },
        }),
      ),
    )
  }

  async function logout(): Promise<void> {
    try {
      await unwrap(api!.client.POST('/api/v1/auth/logout'))
    } catch (error) {
      // Signing out must work offline too. The refresh cookie then simply expires by itself.
      if (!(error instanceof ApiError)) {
        throw error
      }
    } finally {
      clearLocalState()
      broadcast('signed-out')
    }
  }

  /** The session changed on the server, for example after enabling two-factor sign-in. */
  async function refreshSession(): Promise<void> {
    await api!.session.refresh()
    setupRequiredAtLogin.value = false
    await loadProfile()
  }

  async function switchTenant(tenantId: string): Promise<void> {
    const grant = await unwrap(api!.client.POST('/api/v1/account/tenant', { body: { tenantId } }))

    api!.session.accept(grant)
  }

  function can(permission: string | readonly string[]): boolean {
    const held = claims.value?.permissions ?? []
    const wanted = typeof permission === 'string' ? [permission] : permission

    return wanted.some((item) => held.includes(item))
  }

  const isStaff = computed(() =>
    (claims.value?.roles ?? []).some((role) => STAFF_ROLES.includes(role)),
  )

  return {
    status,
    claims,
    profile,
    isStaff,
    // A page reload only has the profile to go by, as a refresh does not repeat the login's answer.
    twoFactorSetupRequired: computed(
      () =>
        setupRequiredAtLogin.value ||
        (isStaff.value && profile.value !== null && !profile.value.twoFactorEnabled),
    ),
    isAuthenticated: computed(() => status.value === 'authenticated'),
    hasActiveTenant: computed(() => Boolean(claims.value?.tenantId)),
    handleExpired: clearLocalState,
    bootstrap,
    login,
    completeTwoFactor,
    logout,
    loadProfile,
    refreshSession,
    switchTenant,
    can,
    onSignedOut(handler: () => void): () => void {
      signedOutHandlers.add(handler)

      return () => signedOutHandlers.delete(handler)
    },
  }
})

export function useApi(): Api {
  const api = inject(API_KEY)

  if (!api) {
    throw new Error('Provide the API with app.provide(API_KEY, api).')
  }

  return api
}
