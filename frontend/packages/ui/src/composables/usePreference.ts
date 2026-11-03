import { onMounted, ref, watch, type Ref } from 'vue'

const PREFIX = 'comtest.pref.'

/**
 * Remembers a harmless interface choice, such as a collapsed sidebar, in this browser. Tokens and
 * personal data do not belong here. Storage can be missing or blocked, and nothing may depend on it.
 */
export function readPreference(key: string): string | null {
  try {
    // eslint-disable-next-line no-restricted-properties -- this is the single allowed access point
    return globalThis.localStorage?.getItem(PREFIX + key) ?? null
  } catch {
    return null
  }
}

export function writePreference(key: string, value: string | null): void {
  try {
    if (value === null) {
      // eslint-disable-next-line no-restricted-properties -- this is the single allowed access point
      globalThis.localStorage?.removeItem(PREFIX + key)
    } else {
      // eslint-disable-next-line no-restricted-properties -- this is the single allowed access point
      globalThis.localStorage?.setItem(PREFIX + key, value)
    }
  } catch {
    // Private windows may refuse. The choice then lasts for this page only.
  }
}

export function usePreference(key: string, fallback: string): Ref<string> {
  const value = ref(fallback)

  // Read after mounting, so that server and browser render the same first frame.
  onMounted(() => {
    value.value = readPreference(key) ?? fallback
  })
  watch(value, (next) => writePreference(key, next))

  return value
}
