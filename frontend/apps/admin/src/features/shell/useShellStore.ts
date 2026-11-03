import { defineStore } from 'pinia'
import { onBeforeUnmount, ref, toValue, watchEffect, type MaybeRefOrGetter } from 'vue'

export const useShellStore = defineStore('shell', () => {
  /** What a page calls itself once its data has loaded, such as an order's number. */
  const pageTitle = ref<string | null>(null)

  return { pageTitle }
})

/** Names the current page in the breadcrumbs and the browser tab. */
export function usePageTitle(title: MaybeRefOrGetter<string | null | undefined>): void {
  const shell = useShellStore()

  watchEffect(() => {
    shell.pageTitle = toValue(title) || null
  })
  onBeforeUnmount(() => {
    shell.pageTitle = null
  })
}
