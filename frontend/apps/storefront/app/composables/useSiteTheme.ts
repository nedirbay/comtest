import type { Theme } from '@comtest/ui'

/**
 * Kept in a cookie instead of localStorage, so the server can render the right theme straight away
 * and dark mode does not start with a white flash. It holds nothing but the word light or dark.
 */
export function useSiteTheme() {
  const theme = useCookie<Theme>('comtest-theme', {
    default: () => 'light',
    maxAge: 60 * 60 * 24 * 365,
    sameSite: 'lax',
  })

  useHead({ htmlAttrs: { class: computed(() => (theme.value === 'dark' ? 'dark' : '')) } })

  return {
    isDark: computed(() => theme.value === 'dark'),
    toggle() {
      theme.value = theme.value === 'dark' ? 'light' : 'dark'
    },
  }
}
