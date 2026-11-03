import { unwrap } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { useQueries } from '@tanstack/vue-query'
import type { Ref } from 'vue'

/** Details for a list of products. One that was removed since is simply left out. */
export function useProductsBySlug(slugs: Ref<string[]>) {
  const api = useApi()
  const { locale } = useI18n()

  const queries = useQueries({
    queries: computed(() =>
      slugs.value.map((slug) => ({
        queryKey: ['catalog', 'product', slug, locale.value],
        queryFn: () =>
          unwrap(api.client.GET('/api/v1/catalog/products/{slug}', { params: { path: { slug } } })),
        retry: false,
      })),
    ),
  })

  return computed(() => queries.value.flatMap((query) => (query.data ? [query.data] : [])))
}
