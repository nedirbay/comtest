import { ApiError } from '@comtest/api-client'
import {
  dehydrate,
  hydrate,
  QueryClient,
  VueQueryPlugin,
  type DehydratedState,
} from '@tanstack/vue-query'

const MAX_RETRIES = 2
const STALE_AFTER_MS = 30_000

/** A new client per request, because a shared one would leak one visitor's data to the next. */
export default defineNuxtPlugin((nuxtApp) => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: STALE_AFTER_MS,
        refetchOnWindowFocus: false,
        retry: (failures, error) =>
          import.meta.client &&
          failures < MAX_RETRIES &&
          !(error instanceof ApiError && !error.isNetworkError && error.status < 500),
      },
      mutations: { retry: false },
    },
  })

  nuxtApp.vueApp.use(VueQueryPlugin, { queryClient })

  if (import.meta.server) {
    nuxtApp.hooks.hook('app:rendered', () => {
      nuxtApp.payload.vueQueryState = dehydrate(queryClient)
    })
  }

  if (import.meta.client && nuxtApp.payload.vueQueryState) {
    hydrate(queryClient, nuxtApp.payload.vueQueryState as DehydratedState)
  }

  return { provide: { queryClient } }
})
