import { computed, reactive, ref, watch, type WritableComputedRef } from 'vue'
import { useRoute, useRouter, type LocationQuery, type LocationQueryRaw } from 'vue-router'

export const DEFAULT_PAGE_SIZE = 20
const ALLOWED_PAGE_SIZES = [20, 50, 100]

export interface TableQueryOptions<Filters extends Record<string, string>> {
  /** Filter names with their empty value. Each one becomes a parameter in the address. */
  filters: Filters
  /** Leave the sort to the server unless this says otherwise. */
  defaultSort?: string
}

function single(value: LocationQuery[string] | undefined): string | undefined {
  const first = Array.isArray(value) ? value[0] : value

  return first ?? undefined
}

/**
 * Keeps a list's page, sort and filters in the address, so that a link shows the same list to a
 * colleague and the back button undoes a filter. Only this page's own address counts: while the
 * router moves on to another page, the previous values stay, and no stray request goes out.
 */
export function useTableQuery<Filters extends Record<string, string>>(
  options: TableQueryOptions<Filters>,
) {
  const route = useRoute()
  const router = useRouter()
  const ownRoute = route.name
  const query = ref<LocationQuery>(route.query)
  const defaultSort = options.defaultSort ?? ''
  // What the last change asked for. A second change can come while the first one is still on its
  // way through the router, and it must build on that one rather than on the old address.
  let latest: LocationQueryRaw = route.query

  watch(
    () => route.query,
    (next) => {
      if (route.name === ownRoute) {
        query.value = next
        latest = next
      }
    },
  )

  function update(patch: Record<string, string | number | undefined>): void {
    const next: LocationQueryRaw = { ...latest }

    for (const [key, value] of Object.entries(patch)) {
      if (value === undefined || value === '') {
        delete next[key]
      } else {
        next[key] = String(value)
      }
    }

    latest = next
    void router.push({ query: next })
  }

  const page = computed<number>({
    get: () => Math.max(Number(single(query.value.page)) || 1, 1),
    set: (value) => update({ page: value > 1 ? value : undefined }),
  })

  const pageSize = computed<number>({
    get: () => {
      const wanted = Number(single(query.value.pageSize))

      return ALLOWED_PAGE_SIZES.includes(wanted) ? wanted : DEFAULT_PAGE_SIZE
    },
    set: (value) =>
      update({ pageSize: value === DEFAULT_PAGE_SIZE ? undefined : value, page: undefined }),
  })

  const sort = computed<string>({
    get: () => single(query.value.sort) ?? defaultSort,
    set: (value) => update({ sort: value === defaultSort ? undefined : value, page: undefined }),
  })

  const filterRefs = Object.fromEntries(
    Object.entries(options.filters).map(([key, empty]) => [
      key,
      computed<string>({
        get: () => single(query.value[key]) ?? empty,
        // A narrower list may not have the page the user was on.
        set: (value) => update({ [key]: value === empty ? undefined : value, page: undefined }),
      }),
    ]),
  ) as { [Key in keyof Filters]: WritableComputedRef<string> }

  const filters = reactive(filterRefs) as { [Key in keyof Filters]: string }

  return reactive({
    page,
    pageSize,
    sort,
    filters,
    hasFilters: computed(() =>
      Object.entries(options.filters).some(
        ([key, empty]) => filters[key as keyof Filters] !== empty,
      ),
    ),
    clearFilters(): void {
      update({
        ...Object.fromEntries(Object.keys(options.filters).map((key) => [key, undefined])),
        page: undefined,
      })
    },
  })
}

export type TableQuery = ReturnType<typeof useTableQuery<Record<string, string>>>
