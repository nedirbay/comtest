import { expect, test as base } from '@playwright/test'
import { test } from './support/fixtures'
import { createProduct } from './support/demo-store'
import { STOREFRONT_URL } from './support/stack'

const url = (path: string) => `${STOREFRONT_URL}${path}`

function jsonLd(html: string): Record<string, unknown>[] {
  return [...html.matchAll(/<script[^>]*application\/ld\+json[^>]*>(.*?)<\/script>/gs)].map(
    (match) => JSON.parse(match[1]!) as Record<string, unknown>,
  )
}

test.describe('search engines', () => {
  test('a product page describes itself', async ({ request }) => {
    const product = await createProduct({
      name: 'SEO köýnek',
      image: true,
      variants: [{ price: 150, compareAt: 200, stock: 3 }],
    })
    const html = await (await request.get(url(`/products/${product.slug}`))).text()

    expect(html).toMatch(/<title>SEO köýnek · Demo Dükan<\/title>/)
    expect(html).toMatch(
      /property="og:title"[^>]*content="SEO köýnek"|content="SEO köýnek"[^>]*property="og:title"/,
    )
    expect(html).toMatch(
      /property="og:type"[^>]*content="product"|content="product"[^>]*property="og:type"/,
    )
    expect(html).toContain('property="og:image"')
    expect(html).toContain(`rel="canonical" href="${url(`/products/${product.slug}`)}"`)

    const data = jsonLd(html)
    const ld = data.find((item) => item['@type'] === 'Product') as {
      name: string
      offers: { price: number; priceCurrency: string; availability: string }[]
      image: string[]
    }

    expect(ld.name).toBe('SEO köýnek')
    expect(ld.offers[0]).toMatchObject({
      price: 150,
      priceCurrency: 'TMT',
      availability: 'https://schema.org/InStock',
    })
    expect(ld.image).toHaveLength(1)
    expect(data.some((item) => item['@type'] === 'BreadcrumbList')).toBe(true)
  })

  test('a store text cannot break out of the structured data', async ({ request }) => {
    const product = await createProduct({
      name: 'Köýnek </script><script>window.hacked=1</script>',
    })
    const html = await (await request.get(url(`/products/${product.slug}`))).text()

    expect(html).not.toContain('<script>window.hacked')
    expect(jsonLd(html).find((item) => item['@type'] === 'Product')?.name).toContain('</script>')
  })

  test('every language version points at the others', async ({ request }) => {
    const html = await (await request.get(url('/ru/catalog/telefonlar'))).text()

    expect(html).toContain(`rel="canonical" href="${url('/ru/catalog/telefonlar')}"`)
    expect(html).toContain(`hreflang="tk-TM" href="${url('/catalog/telefonlar')}"`)
    expect(html).toContain(`hreflang="ru-RU" href="${url('/ru/catalog/telefonlar')}"`)
    expect(html).toContain(`hreflang="en-GB" href="${url('/en/catalog/telefonlar')}"`)
    expect(html).toContain(`hreflang="x-default" href="${url('/catalog/telefonlar')}"`)
  })

  test('filters share the address of the page they filter', async ({ request }) => {
    const html = await (await request.get(url('/catalog?sort=price&maxPrice=100&page=2'))).text()

    expect(html).toContain(`rel="canonical" href="${url('/catalog?page=2')}"`)
  })

  test('the home page names the organization', async ({ request }) => {
    const data = jsonLd(await (await request.get(url('/'))).text())

    expect(data.find((item) => item['@type'] === 'Organization')?.name).toBe('Demo Dükan')
  })

  test('the sitemap lists products and categories in all languages', async ({ request }) => {
    const response = await request.get(url('/sitemap.xml'))
    const xml = await response.text()

    expect(response.headers()['content-type']).toContain('application/xml')
    expect(xml).toContain(`<loc>${url('/products/galaxy-a55')}</loc>`)
    expect(xml).toContain(`<loc>${url('/ru/products/galaxy-a55')}</loc>`)
    expect(xml).toContain(`<loc>${url('/catalog/telefonlar')}</loc>`)
    expect(xml).toContain('hreflang="en-GB"')
    expect(xml).not.toContain('/account')
  })

  test('robots.txt keeps private pages out and names the sitemap', async ({ request }) => {
    const text = await (await request.get(url('/robots.txt'))).text()

    expect(text).toContain('Disallow: /checkout')
    expect(text).toContain('Disallow: /ru/account')
    expect(text).toContain(`Sitemap: ${url('/sitemap.xml')}`)
  })
})

test.describe('security headers', () => {
  test('rendered pages carry a strict policy with a fresh nonce', async ({ request }) => {
    const first = await request.get(url('/'))
    const second = await request.get(url('/'))
    const policy = first.headers()['content-security-policy']!

    expect(policy).toContain("object-src 'none'")
    expect(policy).toContain("frame-ancestors 'none'")
    expect(policy).toContain("base-uri 'self'")
    expect(policy).not.toMatch(/script-src[^;]*unsafe-inline/)
    expect(policy).not.toMatch(/script-src[^;]*unsafe-eval/)

    const nonce = /'nonce-([^']+)'/.exec(policy)?.[1]

    expect(nonce).toBeTruthy()
    expect(second.headers()['content-security-policy']).not.toContain(nonce)
    expect(await first.text()).toContain(`nonce="${nonce}"`)

    expect(first.headers()['x-content-type-options']).toBe('nosniff')
    expect(first.headers()['x-frame-options']).toBe('DENY')
  })

  base('the policy really blocks a script without its nonce', async ({ page }) => {
    await page.goto(url('/'), { waitUntil: 'networkidle' })

    const ran = await page.evaluate(async () => {
      const script = document.createElement('script')

      script.textContent = 'window.injected = true'
      document.head.append(script)
      await new Promise((resolve) => setTimeout(resolve, 50))

      return (window as unknown as { injected?: boolean }).injected === true
    })

    expect(ran).toBe(false)
  })

  test('violation reports are accepted, and odd ones do no harm', async ({ request }) => {
    const report = { 'csp-report': { 'violated-directive': 'script-src', 'blocked-uri': 'inline' } }

    expect((await request.post(url('/_csp-report'), { data: report })).status()).toBe(204)
    expect((await request.post(url('/_csp-report'), { data: 'not json' })).status()).toBe(204)
    expect((await request.post(url('/_csp-report'), { data: 'x'.repeat(20_000) })).status()).toBe(
      204,
    )
  })

  test('no source maps are published', async ({ request }) => {
    const html = await (await request.get(url('/'))).text()
    const script = /src="(\/_nuxt\/[^"]+\.js)"/.exec(html)?.[1]

    expect(script).toBeTruthy()
    expect((await request.get(url(`${script}.map`))).status()).toBe(404)
  })
})
