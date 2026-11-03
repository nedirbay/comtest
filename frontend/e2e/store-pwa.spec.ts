import { expect, test } from './support/fixtures'
import { STOREFRONT_URL } from './support/stack'

test('each store installs under its own name', async ({ request }) => {
  const response = await request.get(`${STOREFRONT_URL}/manifest.webmanifest`)
  const manifest = (await response.json()) as { name: string; start_url: string }

  expect(response.headers()['content-type']).toContain('manifest+json')
  expect(manifest.name).toBe('Demo Dükan')
  expect(manifest.start_url).toBe('/')
})

test.describe('the service worker', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto(`${STOREFRONT_URL}/catalog`, { waitUntil: 'networkidle' })
    await page.evaluate(() => navigator.serviceWorker.ready)
    // The first visit happens before it takes control, and the second one goes through it.
    await page.reload({ waitUntil: 'networkidle' })
    await expect
      .poll(() => page.evaluate(() => Boolean(navigator.serviceWorker.controller)))
      .toBe(true)
  })

  test('keeps only files that can never change or belong to anyone', async ({ page }) => {
    const cached = await page.evaluate(async () => {
      const urls: string[] = []

      for (const name of await caches.keys()) {
        for (const request of await (await caches.open(name)).keys()) {
          urls.push(new URL(request.url).pathname)
        }
      }

      return urls
    })

    expect(cached).toContain('/offline.html')
    expect(cached.some((path) => path.startsWith('/_nuxt/'))).toBe(true)
    expect(
      cached.filter((path) => path !== '/offline.html' && !path.startsWith('/_nuxt/')),
    ).toEqual([])
  })

  test('without a connection pages show the offline message', async ({ page, context }) => {
    await context.setOffline(true)
    await page.goto(`${STOREFRONT_URL}/products/galaxy-a55`).catch(() => undefined)

    await expect(page.getByTestId('offline-page')).toContainText('Internet baglanyşygy ýok')

    await context.setOffline(false)
  })
})
