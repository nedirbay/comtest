import { computed, ref, type ComputedRef, type Ref } from 'vue'
import { readPreference, writePreference } from './usePreference'

export type Theme = 'light' | 'dark'

const KEY = 'theme'

export function applyTheme(theme: Theme): void {
  document.documentElement.classList.toggle('dark', theme === 'dark')
}

export function preferredTheme(): Theme {
  const saved = readPreference(KEY)

  if (saved === 'light' || saved === 'dark') {
    return saved
  }

  return globalThis.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

/** For a client-rendered app. The storefront keeps the choice in a cookie so that SSR can see it. */
export function useTheme(): {
  theme: Ref<Theme>
  isDark: ComputedRef<boolean>
  toggle: () => void
} {
  const theme = ref<Theme>(preferredTheme())

  applyTheme(theme.value)

  return {
    theme,
    isDark: computed(() => theme.value === 'dark'),
    toggle() {
      theme.value = theme.value === 'dark' ? 'light' : 'dark'
      writePreference(KEY, theme.value)
      applyTheme(theme.value)
    },
  }
}
