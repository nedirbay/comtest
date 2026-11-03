import { unwrap } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { useQuery } from '@tanstack/vue-query'
import { onServerPrefetch } from 'vue'

/** The store's public details, rendered on the server. The page still works if the API is down. */
export function useStoreInfo() {
  const api = useApi()
  const { locale } = useI18n()

  // A failed query is not sent to the browser, which would start out as "loading" and so render
  // something else than the server did. This flag keeps the first render of both the same.
  const failedOnServer = useState('store-failed-on-server', () => false)

  const query = useQuery({
    queryKey: ['store', locale],
    queryFn: () => unwrap(api.client.GET('/api/v1/store')),
  })

  onServerPrefetch(async () => {
    // A failed query usually resolves with an error result instead of rejecting. Either can happen.
    const result = await query.suspense().catch(() => null)

    failedOnServer.value = !result || result.isError
  })

  return Object.assign(query, {
    /** Nothing to show: the store does not exist or the API cannot be reached. */
    unavailable: computed(() => !query.data.value && (failedOnServer.value || query.isError.value)),
  })
}
