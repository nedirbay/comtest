import { createApi } from '@comtest/api-client'
import { API_KEY, useAuthStore } from '@comtest/auth'

/**
 * Runs once per server request and once in the browser, so nothing here is shared between visitors.
 * On the server the session stays empty: it has no cookie to read and never makes authorized calls.
 */
export default defineNuxtPlugin({
  name: 'api',
  dependsOn: ['i18n:plugin', 'pinia'],
  async setup(nuxtApp) {
    const config = useRuntimeConfig()
    const { devTenant, apiUrl } = config.public
    const requestHost = import.meta.server ? useRequestHeader('host') : undefined

    // In the browser the Host header is the address bar's, which is already right.
    const fetchForServer =
      import.meta.server && !devTenant && requestHost
        ? (await import('~/utils/fetchWithHost')).createFetchWithHost(requestHost)
        : undefined

    const api = createApi({
      baseUrl: import.meta.server ? config.apiInternalUrl || apiUrl : apiUrl,
      getLocale: () => (nuxtApp.$i18n as { locale: { value: string } }).locale.value,
      getHeaders: (): Record<string, string> => (devTenant ? { 'X-Tenant': devTenant } : {}),
      locks: import.meta.client ? (navigator.locks ?? null) : null,
      onSessionExpired: () => useAuthStore(nuxtApp.$pinia as never).handleExpired(),
      ...(fetchForServer ? { fetch: fetchForServer } : {}),
    })

    nuxtApp.vueApp.provide(API_KEY, api)

    return { provide: { api } }
  },
})
