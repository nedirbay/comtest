import { unwrap } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { useQuery } from '@tanstack/vue-query'

const REFRESH_MS = 60_000
const FINAL_STATUSES = ['delivered', 'cancelled', 'returned']

/**
 * The API has no live channel yet, so an open order page asks again now and then, for as long as
 * the order can still change.
 */
export function orderRefreshInterval(query: {
  state: { data?: { status: string } }
}): number | false {
  const status = query.state.data?.status

  return status && !FINAL_STATUSES.includes(status) ? REFRESH_MS : false
}

/** Asks the server about an order by its signed token. Nothing in the address is trusted by itself. */
export function useOrderTracking(token: Ref<string | null>) {
  const api = useApi()
  const { locale } = useI18n()

  return useQuery({
    queryKey: ['order-tracking', token, locale],
    queryFn: () =>
      unwrap(api.client.POST('/api/v1/orders/track', { body: { token: token.value as string } })),
    enabled: computed(() => Boolean(token.value)),
    retry: false,
    refetchInterval: orderRefreshInterval,
  })
}
