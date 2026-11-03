import { safeReturnUrl, useAuthStore } from '@comtest/auth'

/**
 * Keeps guests away from pages that would only show them errors. This is a courtesy and not a
 * lock: the API checks every request by itself.
 */
export default defineNuxtRouteMiddleware(async (to) => {
  if (import.meta.server) {
    return
  }

  const auth = useAuthStore()

  await auth.bootstrap()

  if (!auth.isAuthenticated) {
    return navigateTo(
      useLocalePath()({ path: '/login', query: { returnUrl: safeReturnUrl(to.fullPath) } }),
    )
  }
})
