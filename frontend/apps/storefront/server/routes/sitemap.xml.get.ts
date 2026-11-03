const PREFIXES: Record<string, string> = { 'tk-TM': '', 'ru-RU': '/ru', 'en-GB': '/en' }
const PAGE_SIZE = 100
/** The sitemap format's own limit is 50,000 addresses, and each product has three languages. */
const MAX_PAGES = 150
const HOUR_SECONDS = 3600

interface CategoryNode {
  slug: string
  children: CategoryNode[]
}

const escapeXml = (value: string) => value.replace(/[<>&'"]/g, (char) => `&#${char.charCodeAt(0)};`)

function flatten(nodes: CategoryNode[]): string[] {
  return nodes.flatMap((node) => [node.slug, ...flatten(node.children)])
}

/** Every store has its own sitemap, because the host name decides whose catalog the API returns. */
export default defineEventHandler(async (event) => {
  const { origin } = getRequestURL(event, { xForwardedHost: true })
  const get = <T>(path: string) => storeApiGet<T>(event, path)

  const paths = ['/', '/catalog']
  const categories = await get<CategoryNode[]>('/catalog/categories')

  paths.push(...flatten(categories).map((slug) => `/catalog/${encodeURIComponent(slug)}`))

  for (let page = 1; page <= MAX_PAGES; page += 1) {
    const result = await get<{ items: { slug: string }[]; totalCount: number }>(
      `/catalog/products?page=${page}&pageSize=${PAGE_SIZE}`,
    )

    paths.push(...result.items.map((item) => `/products/${encodeURIComponent(item.slug)}`))

    if (page * PAGE_SIZE >= result.totalCount) {
      break
    }
  }

  const addressIn = (prefix: string, path: string) =>
    escapeXml(`${origin}${prefix}${path === '/' && prefix ? '' : path}`)

  const entries = paths.flatMap((path) =>
    Object.values(PREFIXES).map((prefix) => {
      const alternates = Object.entries(PREFIXES)
        .map(
          ([language, other]) =>
            `<xhtml:link rel="alternate" hreflang="${language}" href="${addressIn(other, path)}"/>`,
        )
        .join('')

      return `<url><loc>${addressIn(prefix, path)}</loc>${alternates}</url>`
    }),
  )

  setResponseHeaders(event, {
    'Content-Type': 'application/xml; charset=utf-8',
    'Cache-Control': `public, max-age=${HOUR_SECONDS}`,
  })

  return `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">${entries.join('')}</urlset>\n`
})
