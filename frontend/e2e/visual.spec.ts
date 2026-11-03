import { expect, test } from './support/fixtures'
import { ADMIN_URL, STOREFRONT_URL } from './support/stack'

/**
 * Pages whose look does not depend on test data. A real difference here is either a deliberate
 * redesign, then `pnpm test:visual:update`, or something broke, like the cascade layers once did.
 */
const PAGES = [
  ['store-login', `${STOREFRONT_URL}/login`],
  ['store-register', `${STOREFRONT_URL}/register`],
  ['store-forgot-password', `${STOREFRONT_URL}/forgot-password`],
  ['store-404', `${STOREFRONT_URL}/no/such/page`],
  ['store-empty-cart', `${STOREFRONT_URL}/cart`],
  ['admin-login', `${ADMIN_URL}/tk/login`],
  ['admin-forgot-password', `${ADMIN_URL}/tk/forgot-password`],
] as const

for (const [name, url] of PAGES) {
  for (const theme of ['light', 'dark'] as const) {
    test(`${name}, ${theme}`, async ({ page, context }) => {
      await context.addCookies([{ name: 'comtest-theme', value: theme, url: STOREFRONT_URL }])
      await page.emulateMedia({ colorScheme: theme })
      await page.goto(url, { waitUntil: 'networkidle' })
      await page.evaluate(() => document.fonts.ready)

      await expect(page).toHaveScreenshot(`${name}-${theme}.png`, {
        fullPage: true,
        animations: 'disabled',
        maxDiffPixelRatio: 0.01,
      })
    })
  }
}
