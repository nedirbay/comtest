import { gzipSync } from 'node:zlib'
import type { CDPSession, Page } from '@playwright/test'
import { expect, test } from './support/fixtures'
import { STOREFRONT_URL } from './support/stack'

/** What the page itself may weigh, gzipped. Pictures come from stores and are not counted. */
const BUDGET = { js: 250_000, css: 60_000, fonts: 80_000 }

/** Chrome's "Fast 3G" and a phone that is four times slower than this computer. */
const SLOW_NETWORK = {
  offline: false,
  latency: 150,
  downloadThroughput: (1.6 * 1024 * 1024) / 8,
  uploadThroughput: (750 * 1024) / 8,
}

async function slowDown(page: Page): Promise<CDPSession> {
  const cdp = await page.context().newCDPSession(page)

  await cdp.send('Network.enable')
  await cdp.send('Network.emulateNetworkConditions', SLOW_NETWORK)
  await cdp.send('Emulation.setCPUThrottlingRate', { rate: 4 })

  return cdp
}

/** Starts collecting before anything loads, as the first paints would otherwise be missed. */
async function watchVitals(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const vitals = { lcp: 0, cls: 0, inp: 0 }

    Object.assign(window, { __vitals: vitals })

    new PerformanceObserver((list) => {
      vitals.lcp = list.getEntries().at(-1)?.startTime ?? vitals.lcp
    }).observe({ type: 'largest-contentful-paint', buffered: true })

    new PerformanceObserver((list) => {
      for (const entry of list.getEntries() as (PerformanceEntry & {
        value: number
        hadRecentInput: boolean
      })[]) {
        if (!entry.hadRecentInput) {
          vitals.cls += entry.value
        }
      }
    }).observe({ type: 'layout-shift', buffered: true })

    new PerformanceObserver((list) => {
      for (const entry of list.getEntries()) {
        vitals.inp = Math.max(vitals.inp, entry.duration)
      }
    }).observe({ type: 'event', buffered: true, durationThreshold: 16 } as PerformanceObserverInit)
  })
}

const readVitals = (page: Page) =>
  page.evaluate(
    () => (window as unknown as { __vitals: { lcp: number; cls: number; inp: number } }).__vitals,
  )

// One measurement at a time, because parallel browsers would slow each other down.
test.describe.configure({ mode: 'serial' })

for (const path of ['/', '/catalog/telefonlar', '/products/galaxy-a55']) {
  test(`${path} stays within its size budget`, async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop')

    const sizes = { js: 0, css: 0, fonts: 0 }
    const pending: Promise<void>[] = []

    page.on('response', (response) => {
      const url = response.url()
      const kind = /\.js(\?|$)/.test(url)
        ? 'js'
        : /\.css(\?|$)/.test(url)
          ? 'css'
          : /\.woff2(\?|$)/.test(url)
            ? 'fonts'
            : null

      if (kind) {
        pending.push(
          response.body().then((body) => {
            sizes[kind] += gzipSync(body).length
          }),
        )
      }
    })

    await page.goto(`${STOREFRONT_URL}${path}`, { waitUntil: 'networkidle' })
    await Promise.all(pending)

    testInfo.annotations.push({ type: 'size', description: JSON.stringify(sizes) })
    console.log(`${path}: ${JSON.stringify(sizes)}`)

    expect(sizes.js).toBeLessThan(BUDGET.js)
    expect(sizes.css).toBeLessThan(BUDGET.css)
    expect(sizes.fonts).toBeLessThan(BUDGET.fonts)
  })

  test(`${path} meets the Core Web Vitals on a slow phone connection`, async ({
    page,
  }, testInfo) => {
    test.skip(testInfo.project.name !== 'phone')
    test.slow()

    await watchVitals(page)
    await slowDown(page)
    await page.goto(`${STOREFRONT_URL}${path}`, { waitUntil: 'networkidle' })

    // A real interaction to measure. The browser only records those above 16 ms, so zero is a pass.
    await page.getByRole('button', { name: /Dil/ }).click()
    await page.waitForTimeout(500)

    const vitals = await readVitals(page)

    console.log(`${path}: ${JSON.stringify(vitals)}`)

    expect(vitals.lcp).toBeLessThan(2500)
    expect(vitals.cls).toBeLessThan(0.1)
    expect(vitals.inp).toBeLessThan(200)
  })
}

test('the page is readable before its JavaScript has loaded', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'phone')
  test.slow()

  await slowDown(page)
  await page.route('**/*.js', () => {
    // Never answered, like a script stuck on a bad connection.
  })
  await page.goto(`${STOREFRONT_URL}/products/galaxy-a55`, { waitUntil: 'commit' })

  await expect(page.getByTestId('product-name')).toHaveText('Galaxy A55')
  await expect(page.getByTestId('price').first()).toContainText('5')
})
