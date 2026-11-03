import { unwrap } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { useQuery } from '@tanstack/vue-query'

/** The regions this store delivers to, as codes. `regionLabel` turns one into a name. */
export function useRegions() {
  const api = useApi()
  const { t, te } = useI18n()

  const query = useQuery({
    queryKey: ['shipping', 'regions'],
    queryFn: () => unwrap(api.client.GET('/api/v1/shipping/regions')),
    staleTime: 10 * 60_000,
  })

  return {
    regions: query.data,
    isPending: query.isPending,
    regionLabel: (code: string | null | undefined) => {
      if (!code) {
        return ''
      }

      return te(`regions.${code}`) ? t(`regions.${code}`) : code
    },
  }
}
