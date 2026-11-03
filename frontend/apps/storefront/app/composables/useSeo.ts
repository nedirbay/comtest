import { toValue, type MaybeRefOrGetter } from 'vue'

export interface PageSeo {
  title?: string | null
  description?: string | null
  image?: string | null
  type?: 'website' | 'product'
}

/** A page's title, description and how it looks when shared on social networks. */
export function usePageSeo(page: MaybeRefOrGetter<PageSeo>) {
  const seo = computed(() => toValue(page))

  useHead({ title: computed(() => seo.value.title ?? undefined) })
  useSeoMeta({
    description: () => seo.value.description ?? undefined,
    ogTitle: () => seo.value.title ?? undefined,
    ogDescription: () => seo.value.description ?? undefined,
    ogImage: () => seo.value.image ?? undefined,
    // `product` is what shops use, though the head library's list of types leaves it out.
    ogType: () => (seo.value.type ?? 'website') as 'website',
    twitterCard: () => (seo.value.image ? 'summary_large_image' : 'summary'),
  })
}

/**
 * The canonical address and its versions in the other languages. A store's address is known only
 * from the request, as every store has its own host name. Filters and sorting do not make a new
 * page for search engines, while the page number does.
 */
export function useLanguageLinks() {
  const route = useRoute()
  const switchLocalePath = useSwitchLocalePath()
  const { locales, locale } = useI18n()
  const origin = useRequestURL({ xForwardedHost: true }).origin

  useHead({
    link: computed(() => {
      const page = Number(route.query.page)
      const suffix = Number.isInteger(page) && page > 1 ? `?page=${page}` : ''
      // The helper keeps the current query string, and filters are not part of a page's address.
      const addressIn = (code: 'tk' | 'ru' | 'en') =>
        `${origin}${switchLocalePath(code).split('?')[0]}${suffix}`

      return [
        { rel: 'canonical' as const, href: addressIn(locale.value as 'tk' | 'ru' | 'en') },
        ...locales.value.map((item) => ({
          rel: 'alternate' as const,
          hreflang: item.language as string,
          href: addressIn(item.code),
        })),
        { rel: 'alternate' as const, hreflang: 'x-default', href: addressIn('tk') },
      ]
    }),
  })

  useSeoMeta({ ogUrl: () => `${origin}${route.path}` })
}

/**
 * Structured data for search engines. A store's own text ends up inside a script element, and a
 * name containing `</script>` would otherwise close it and put the rest of the text on the page.
 */
export function useJsonLd(key: string, data: MaybeRefOrGetter<object | null | undefined>) {
  useHead({
    script: computed(() => {
      const value = toValue(data)

      return value
        ? [
            {
              key: `ld-${key}`,
              type: 'application/ld+json',
              innerHTML: JSON.stringify({ '@context': 'https://schema.org', ...value }).replaceAll(
                '<',
                '\\u003c',
              ),
            },
          ]
        : []
    }),
  })
}

export function breadcrumbLd(items: { label: string; to?: string }[], origin: string) {
  return {
    '@type': 'BreadcrumbList',
    itemListElement: items.map((item, index) => ({
      '@type': 'ListItem',
      position: index + 1,
      name: item.label,
      ...(item.to ? { item: `${origin}${item.to}` } : {}),
    })),
  }
}
