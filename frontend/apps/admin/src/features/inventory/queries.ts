import { unwrap } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { useQuery } from '@tanstack/vue-query'

export const WAREHOUSES_KEY = ['admin', 'inventory', 'warehouses']
export const STOCK_KEY = ['admin', 'inventory', 'stock']
export const MOVEMENTS_KEY = ['admin', 'inventory', 'movements']

export function useWarehouses() {
  const api = useApi()

  return useQuery({
    queryKey: WAREHOUSES_KEY,
    queryFn: () => unwrap(api.client.GET('/api/v1/admin/inventory/warehouses')),
  })
}
