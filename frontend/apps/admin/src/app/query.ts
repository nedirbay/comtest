import { ApiError } from '@comtest/api-client'
import { QueryClient } from '@tanstack/vue-query'

const MAX_RETRIES = 2
const STALE_AFTER_MS = 30_000

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: STALE_AFTER_MS,
      refetchOnWindowFocus: false,
      // Asking again cannot fix a 4xx. Only connection trouble and server errors are worth it.
      retry: (failures, error) =>
        failures < MAX_RETRIES &&
        !(error instanceof ApiError && !error.isNetworkError && error.status < 500),
    },
    mutations: { retry: false },
  },
})
