import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { keepPreviousData, useQuery } from '@tanstack/vue-query'
import { onServerPrefetch, toValue, type MaybeRefOrGetter } from 'vue'

export type StoreCategory = Schema<'StoreCategoryDto'>

export interface ProductQuery {
  page?: number
  pageSize?: number
  sort?: string
  categoryId?: string
  brandId?: string
  minPrice?: number
  maxPrice?: number
  q?: string
}

const CATALOG_STALE_MS = 5 * 60_000

/** Waits for the query during server rendering. A failed one is left to the page's error state. */
function prefetchOnServer(query: { suspense: () => Promise<unknown> }): void {
  onServerPrefetch(async () => {
    await query.suspense().catch(() => undefined)
  })
}

export function useCategories() {
  const api = useApi()
  const { locale } = useI18n()

  const query = useQuery({
    queryKey: ['catalog', 'categories', locale],
    queryFn: () => unwrap(api.client.GET('/api/v1/catalog/categories')),
    staleTime: CATALOG_STALE_MS,
  })

  prefetchOnServer(query)

  return query
}

export function useProducts(params: MaybeRefOrGetter<ProductQuery>) {
  const api = useApi()
  const { locale } = useI18n()

  const query = useQuery({
    queryKey: ['catalog', 'products', params, locale],
    queryFn: () =>
      unwrap(api.client.GET('/api/v1/catalog/products', { params: { query: toValue(params) } })),
    placeholderData: keepPreviousData,
  })

  prefetchOnServer(query)

  return query
}

export function useProduct(slug: MaybeRefOrGetter<string>) {
  const api = useApi()
  const { locale } = useI18n()

  return useQuery({
    queryKey: ['catalog', 'product', slug, locale],
    queryFn: () =>
      unwrap(
        api.client.GET('/api/v1/catalog/products/{slug}', {
          params: { path: { slug: toValue(slug) } },
        }),
      ),
  })
}

export interface CategoryMatch {
  category: StoreCategory
  /** From the top of the tree down to the category itself. */
  trail: StoreCategory[]
}

export function findCategoryBySlug(
  tree: readonly StoreCategory[],
  slug: string,
  trail: StoreCategory[] = [],
): CategoryMatch | null {
  return findCategory(tree, (category) => category.slug === slug, trail)
}

export function findCategoryById(
  tree: readonly StoreCategory[],
  id: string,
  trail: StoreCategory[] = [],
): CategoryMatch | null {
  return findCategory(tree, (category) => category.id === id, trail)
}

function findCategory(
  tree: readonly StoreCategory[],
  matches: (category: StoreCategory) => boolean,
  trail: StoreCategory[],
): CategoryMatch | null {
  for (const category of tree) {
    const path = [...trail, category]

    if (matches(category)) {
      return { category, trail: path }
    }

    const found = findCategory(category.children, matches, path)

    if (found) {
      return found
    }
  }

  return null
}
