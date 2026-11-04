/** What an address can hold for one name. Written out, as the router's own type varies by version. */
type QueryValue = string | null | (string | null)[] | undefined

export const SORT_OPTIONS = ['-publishedAt', 'price', '-price'] as const
export type SortOption = (typeof SORT_OPTIONS)[number]

export interface ListingFilters {
  page: number
  sort: SortOption
  minPrice?: number
  maxPrice?: number
  q?: string
  brandId?: string
  /** `code:value` pairs, as the API reads them. */
  attr?: string[]
}

const MAX_ATTRIBUTE_FILTERS = 30

const first = (value: QueryValue) => (Array.isArray(value) ? value[0] : value) ?? undefined

function attributePairs(value: QueryValue): string[] | undefined {
  const pairs = (Array.isArray(value) ? value : [value])
    .filter((item): item is string => !!item && /^[^:]+:.+$/.test(item))
    .slice(0, MAX_ATTRIBUTE_FILTERS)

  return pairs.length ? pairs : undefined
}

function positiveNumber(value: QueryValue): number | undefined {
  const number = Number(first(value))

  return Number.isFinite(number) && number > 0 ? number : undefined
}

/** Anyone can edit an address, so nothing read from it is trusted to be well formed. */
export function parseListingQuery(query: Record<string, QueryValue>): ListingFilters {
  const sort = first(query.sort)
  const page = Math.floor(positiveNumber(query.page) ?? 1)
  const q = first(query.q)?.trim()
  const brandId = first(query.brandId)

  return {
    page,
    sort: SORT_OPTIONS.includes(sort as SortOption) ? (sort as SortOption) : '-publishedAt',
    minPrice: positiveNumber(query.minPrice),
    maxPrice: positiveNumber(query.maxPrice),
    q: q || undefined,
    brandId: brandId && /^[0-9a-f-]{36}$/i.test(brandId) ? brandId : undefined,
    attr: attributePairs(query.attr),
  }
}

/**
 * The address is the only place filters live, so a filtered page can be reloaded, shared and reached
 * with the back button.
 */
export function useListingFilters() {
  const route = useRoute()
  const router = useRouter()

  const filters = computed(() => parseListingQuery(route.query))

  function apply(change: Partial<ListingFilters>): void {
    const next: Record<string, string | string[]> = {}

    // Any change other than the page itself can make the current page number meaningless.
    for (const [key, value] of Object.entries({ ...filters.value, page: 1, ...change })) {
      if (Array.isArray(value)) {
        if (value.length) {
          next[key] = value
        }
      } else if (value !== undefined && value !== '') {
        next[key] = String(value)
      }
    }

    if (next.page === '1') {
      delete next.page
    }

    if (next.sort === '-publishedAt') {
      delete next.sort
    }

    void router.push({ query: next })
  }

  return {
    filters,
    apply,
    activeCount: computed(
      () =>
        [filters.value.minPrice, filters.value.maxPrice, filters.value.brandId].filter(Boolean)
          .length + (filters.value.attr?.length ?? 0),
    ),
    clear(): void {
      void router.push({ query: filters.value.q ? { q: filters.value.q } : {} })
    },
  }
}
