import { readPreference, writePreference } from '@comtest/ui'

const MAX_RECENT = 8
const MAX_COMPARE = 4

function readList(key: string): string[] {
  try {
    const value: unknown = JSON.parse(readPreference(key) ?? '[]')

    return Array.isArray(value) ? value.filter((item) => typeof item === 'string') : []
  } catch {
    return []
  }
}

/**
 * A product's address name says nothing about who looked at it unless someone reads this browser,
 * so these lists may stay here. They start empty on the server and fill in after mounting, so the
 * first render is the same on both sides.
 */
function useStoredList(key: string) {
  const items = useState<string[]>(`product-list-${key}`, () => [])

  onMounted(() => {
    items.value = readList(key)
  })

  return {
    items,
    save(next: string[]): void {
      items.value = next
      writePreference(key, JSON.stringify(next))
    },
  }
}

export function useRecentlyViewed() {
  const { items, save } = useStoredList('recent-products')

  return {
    items,
    remember(slug: string): void {
      save(
        [slug, ...readList('recent-products').filter((item) => item !== slug)].slice(0, MAX_RECENT),
      )
    },
  }
}

export function useCompareList() {
  const { items, save } = useStoredList('compare-products')

  return {
    items,
    has: (slug: string) => items.value.includes(slug),
    isFull: computed(() => items.value.length >= MAX_COMPARE),
    toggle(slug: string): void {
      if (items.value.includes(slug)) {
        save(items.value.filter((item) => item !== slug))
      } else if (items.value.length < MAX_COMPARE) {
        save([...items.value, slug])
      }
    },
    clear: () => save([]),
  }
}
