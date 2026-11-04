import AxeBuilder from '@axe-core/playwright'
import type { Page } from '@playwright/test'
import { signInAsNewOwner } from './support/admin'
import { expect, test } from './support/fixtures'
import { ADMIN_URL, createConfirmedUser, PASSWORD, STOREFRONT_URL } from './support/stack'

const TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa']

async function expectNoViolations(page: Page): Promise<void> {
  const { violations } = await new AxeBuilder({ page }).withTags(TAGS).analyze()

  expect(
    violations.map((item) => ({
      rule: item.id,
      impact: item.impact,
      nodes: item.nodes
        .slice(0, 3)
        .map(
          (node) => `${node.target.join(' ')} :: ${node.failureSummary?.split('\n')[1]?.trim()}`,
        ),
    })),
  ).toEqual([])
}

const STORE_PAGES = [
  '/',
  '/catalog',
  '/catalog/telefonlar',
  '/products/galaxy-a55',
  '/search?q=Galaxy',
  '/cart',
  '/checkout',
  '/login',
  '/register',
  '/forgot-password',
  '/confirm-email-change',
  '/track',
  '/compare',
  '/no/such/page',
]

for (const theme of ['light', 'dark'] as const) {
  test.describe(`storefront, ${theme} theme`, () => {
    test.beforeEach(async ({ context }) => {
      await context.addCookies([{ name: 'comtest-theme', value: theme, url: STOREFRONT_URL }])
    })

    for (const path of STORE_PAGES) {
      test(`${path} has no WCAG violations`, async ({ page }) => {
        await page.goto(`${STOREFRONT_URL}${path}`, { waitUntil: 'networkidle' })
        await expectNoViolations(page)
      })
    }
  })
}

test('the signed-in customer pages have no WCAG violations', async ({ page }) => {
  test.slow()

  const email = await createConfirmedUser()

  await page.goto(`${STOREFRONT_URL}/login?returnUrl=/account/security`)
  await page.getByLabel('E-poçta ýa-da telefon').fill(email)
  await page.locator('input[type="password"]').fill(PASSWORD)
  await page.getByRole('button', { name: 'Girmek', exact: true }).click()
  await expect(page).toHaveURL(/account\/security/)

  for (const path of ['/account', '/account/orders', '/account/addresses', '/account/security']) {
    await page.goto(`${STOREFRONT_URL}${path}`, { waitUntil: 'networkidle' })
    await expectNoViolations(page)
  }
})

const ADMIN_PAGES = [
  '',
  'orders',
  'customers',
  'products',
  'products/new',
  'categories',
  'brands',
  'attributes',
  'stock',
  'stock/movements',
  'warehouses',
  'settings',
  'staff',
  'security',
]

test('the admin pages have no WCAG violations, in either theme', async ({ page }) => {
  test.setTimeout(240_000)

  await signInAsNewOwner(page, 'Elýeterli Dükan')

  for (const theme of ['light', 'dark']) {
    if (theme === 'dark') {
      await page.getByRole('button', { name: 'Gara tema' }).click()
    }

    for (const path of ADMIN_PAGES) {
      await test.step(`${theme}: /${path}`, async () => {
        await page.goto(`${ADMIN_URL}/tk/${path}`, { waitUntil: 'networkidle' })
        await expectNoViolations(page)
      })
    }
  }
})

test.describe('with only a keyboard', () => {
  test('the first Tab reaches the skip link, which moves past the header', async ({ page }) => {
    await page.goto(`${STOREFRONT_URL}/catalog`, { waitUntil: 'networkidle' })
    await page.keyboard.press('Tab')

    const skip = page.getByRole('link', { name: 'Esasy mazmuna geç' })

    await expect(skip).toBeFocused()
    await expect(skip).toBeVisible()
    await page.keyboard.press('Enter')
    await expect(page).toHaveURL(/#main$/)
  })

  test('someone can sign in without a mouse', async ({ page }) => {
    const email = await createConfirmedUser()

    await page.goto(`${STOREFRONT_URL}/login`, { waitUntil: 'networkidle' })
    await page.getByLabel('E-poçta ýa-da telefon').focus()
    await page.keyboard.type(email)
    await page.keyboard.press('Tab')
    await page.keyboard.type(PASSWORD)
    await page.keyboard.press('Enter')

    await expect(page).toHaveURL(/\/account$/)
  })

  test('a product can be added to the cart, and the drawer closes with Escape', async ({
    page,
  }) => {
    await page.goto(`${STOREFRONT_URL}/products/galaxy-a55`, { waitUntil: 'networkidle' })
    await page.getByTestId('add-to-cart').focus()
    await page.keyboard.press('Enter')

    const drawer = page.getByRole('dialog')

    await expect(drawer).toBeVisible()
    await page.keyboard.press('Escape')
    await expect(drawer).toBeHidden()
  })

  test('focus is visible on links and buttons', async ({ page }) => {
    await page.goto(`${STOREFRONT_URL}/login`, { waitUntil: 'networkidle' })
    await page.getByRole('link', { name: 'Açar sözüni unutdyňyzmy?' }).focus()

    const outline = await page.evaluate(() => {
      const style = getComputedStyle(document.activeElement as Element)

      return { width: style.outlineWidth, line: style.outlineStyle }
    })

    expect(parseFloat(outline.width)).toBeGreaterThanOrEqual(2)
    expect(outline.line).not.toBe('none')
  })

  test('the admin sign-in works without a mouse too', async ({ page }) => {
    const email = await createConfirmedUser()

    await page.goto(`${ADMIN_URL}/tk/login`, { waitUntil: 'networkidle' })
    await page.getByLabel('E-poçta ýa-da telefon').focus()
    await page.keyboard.type(email)
    await page.keyboard.press('Tab')
    await page.keyboard.type(PASSWORD)
    await page.keyboard.press('Enter')

    await expect(page).toHaveURL(/select-store/)
  })
})

test('text scaled to double size does not push the page sideways', async ({ page }) => {
  await page.setViewportSize({ width: 640, height: 900 })
  await page.goto(`${STOREFRONT_URL}/catalog`, { waitUntil: 'networkidle' })
  await page.evaluate(() => {
    document.documentElement.style.fontSize = '200%'
  })

  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  )

  expect(overflow).toBeLessThanOrEqual(0)
})
