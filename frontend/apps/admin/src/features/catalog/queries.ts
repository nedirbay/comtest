import { unwrap } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { useQuery } from '@tanstack/vue-query'

export const CATEGORIES_KEY = ['admin', 'catalog', 'categories']
export const BRANDS_KEY = ['admin', 'catalog', 'brands']
export const ATTRIBUTES_KEY = ['admin', 'catalog', 'attributes']
export const PRODUCTS_KEY = ['admin', 'catalog', 'products']

export function useCategories() {
  const api = useApi()

  return useQuery({
    queryKey: CATEGORIES_KEY,
    queryFn: () => unwrap(api.client.GET('/api/v1/admin/catalog/categories')),
  })
}

export function useBrands() {
  const api = useApi()

  return useQuery({
    queryKey: BRANDS_KEY,
    queryFn: () => unwrap(api.client.GET('/api/v1/admin/catalog/brands')),
  })
}

export function useAttributes() {
  const api = useApi()

  return useQuery({
    queryKey: ATTRIBUTES_KEY,
    queryFn: () => unwrap(api.client.GET('/api/v1/admin/catalog/attributes')),
  })
}

/** The store's public details. Any member may read them, unlike the settings endpoint. */
export function useStoreInfo() {
  const api = useApi()

  return useQuery({
    queryKey: ['admin', 'store', 'public'],
    queryFn: () => unwrap(api.client.GET('/api/v1/store')),
  })
}
