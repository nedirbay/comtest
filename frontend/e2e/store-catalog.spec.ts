import type { Page } from '@playwright/test'
import { expect, test } from './support/fixtures'
import { createProduct, type CreatedProduct } from './support/demo-store'
import { STOREFRONT_URL } from './support/stack'

const url = (path: string) => `${STOREFRONT_URL}${path}`

/** Waits for the JavaScript as well, since clicks on server-rendered HTML go nowhere until it runs. */
const open = (page: Page, path: string) => page.goto(url(path), { waitUntil: 'networkidle' })

test.describe('browsing', () => {
  test('the home page offers categories and new products', async ({ page }) => {
    await open(page, '/')

    await expect(page.getByTestId('store-hero')).toContainText('Demo Dükan')
    await expect(page.getByRole('link', { name: 'Elektronika' }).first()).toBeVisible()
    await expect(page.getByTestId('product-card').first()).toBeVisible()
  })

  test('product names are in the HTML the server sends, for search engines', async ({
    request,
  }) => {
    const home = await (await request.get(url('/'))).text()
    const category = await (await request.get(url('/catalog/telefonlar'))).text()

    expect(home).toContain('data-testid="product-card"')
    expect(category).toContain('Galaxy A55')
    expect(category).toContain('data-testid="price"')
  })

  test('the category page shows its products and where it sits in the tree', async ({ page }) => {
    await open(page, '/catalog/telefonlar')

    await expect(page.getByRole('heading', { name: 'Telefonlar' })).toBeVisible()
    await expect(page.getByRole('navigation', { name: 'Ýol görkeziji' })).toContainText(
      'Elektronika',
    )
    await expect(page.getByTestId('product-card').filter({ hasText: 'Galaxy A55' })).toBeVisible()
  })

  test('unknown categories and products answer 404', async ({ request }) => {
    expect((await request.get(url('/catalog/no-such-category'))).status()).toBe(404)
    expect((await request.get(url('/products/no-such-product'))).status()).toBe(404)
  })

  test('a product card leads to its page', async ({ page }) => {
    await open(page, '/catalog/telefonlar')
    await page.getByTestId('product-card').filter({ hasText: 'Galaxy A55' }).click()

    await expect(page).toHaveURL(/\/products\/galaxy-a55$/)
    await expect(page.getByTestId('product-name')).toHaveText('Galaxy A55')
  })
})

test.describe('filters live in the address', () => {
  test('the price filter survives a reload and the back button', async ({ page }, testInfo) => {
    await open(page, '/search?q=Galaxy')

    const isPhone = testInfo.project.name === 'phone'

    if (isPhone) {
      await page.getByTestId('open-filters').click()
    }

    const panel = isPhone ? page.getByRole('dialog') : page.locator('aside')

    await panel.getByTestId('max-price').locator('input').fill('6000')
    await panel.getByRole('button', { name: 'Ulan' }).click()

    await expect(page).toHaveURL(/maxPrice=6000/)
    await expect(page.getByTestId('product-card').filter({ hasText: 'Galaxy S25' })).toHaveCount(0)
    await expect(page.getByTestId('product-card').filter({ hasText: 'Galaxy A55' })).toBeVisible()

    await page.reload()
    await expect(page.getByTestId('product-card').filter({ hasText: 'Galaxy S25' })).toHaveCount(0)

    await page.goBack()
    await expect(page).not.toHaveURL(/maxPrice/)
    await expect(page.getByTestId('product-card').filter({ hasText: 'Galaxy S25' })).toBeVisible()
  })

  test('sorting changes the order and the address', async ({ page }) => {
    await open(page, '/search?q=Galaxy&sort=-price')

    const names = page.getByTestId('product-card').getByRole('heading')

    await expect(names.first()).toHaveText('Galaxy S25')

    await open(page, '/search?q=Galaxy&sort=price')
    await expect(names.first()).toHaveText('Galaxy A55')
  })

  test('nonsense in the address is ignored instead of breaking the page', async ({ page }) => {
    const response = await open(page, '/catalog?page=-4&sort=evil&minPrice=abc&brandId=x')

    expect(response?.status()).toBe(200)
    await expect(page.getByTestId('product-card').first()).toBeVisible()
  })
})

test.describe('search', () => {
  test('finds products by name', async ({ page }, testInfo) => {
    await open(page, '/')

    if (testInfo.project.name === 'phone') {
      await page.getByTestId('bottom-nav').getByRole('link', { name: 'Gözleg' }).click()
      await expect(page).toHaveURL(/\/search$/)
    }

    const input = page.getByTestId('search-input').locator('visible=true')

    await input.fill('Galaxy S25')
    await input.press('Enter')

    await expect(page).toHaveURL(/\/search\?q=Galaxy/)
    await expect(page.getByTestId('product-card')).toHaveCount(1)
    await expect(page.getByTestId('product-card')).toContainText('Galaxy S25')
  })

  test('says so when nothing is found', async ({ page }) => {
    await open(page, '/search?q=zzzznothingmatches')

    await expect(page.getByText('Harytlar tapylmady')).toBeVisible()
  })

  test('search results are kept out of search engines', async ({ request }) => {
    const html = await (await request.get(url('/search?q=galaxy'))).text()

    expect(html).toMatch(/<meta[^>]+name="robots"[^>]+noindex/)
  })
})

test.describe('finding your way back', () => {
  test('typing suggests products', async ({ page }, testInfo) => {
    await open(page, testInfo.project.name === 'phone' ? '/search' : '/')

    const input = page.getByTestId('search-input').locator('visible=true')

    await input.pressSequentially('Galaxy S2', { delay: 20 })
    await page.getByTestId('search-suggestion').filter({ hasText: 'Galaxy S25' }).click()

    await expect(page).toHaveURL(/\/products\/galaxy-s25$/)
  })

  test('recently viewed products are remembered, and similar ones offered', async ({ page }) => {
    await open(page, '/products/galaxy-a55')
    await expect(page.getByTestId('similar-products')).toContainText('Galaxy S25')

    await open(page, '/products/galaxy-s25')
    await expect(page.getByTestId('recently-viewed')).toContainText('Galaxy A55')
  })

  test('products can be compared side by side', async ({ page }) => {
    await open(page, '/products/galaxy-a55')
    await page.getByTestId('compare-toggle').click()
    await open(page, '/products/galaxy-s25')
    await page.getByTestId('compare-toggle').click()

    await open(page, '/compare')

    const table = page.getByTestId('compare-table')

    await expect(table).toContainText('Galaxy A55')
    await expect(table).toContainText('Galaxy S25')
    await expect(table).toContainText('5\u00a0200,00')

    await page.getByTestId('compare-clear').click()
    await expect(page.getByText('Deňeşdirmek üçin haryt ýok')).toBeVisible()
  })
})

test.describe('the product page', () => {
  let shirt: CreatedProduct

  test.beforeAll(async () => {
    shirt = await createProduct({
      image: true,
      description:
        '<p>Gowy <strong>köýnek</strong></p><script>window.hacked = true</script><img src="x" onerror="window.hacked = true">',
      variants: [
        { price: 300, stock: 5, options: { color: 'gyzyl', size: 'M' } },
        { price: 320, compareAt: 400, stock: 5, options: { color: 'gök', size: 'M' } },
        { price: 340, stock: 0, options: { color: 'gök', size: 'XL' } },
      ],
    })
  })

  test('choosing a variant changes the price and stock state', async ({ page }) => {
    await open(page, `/products/${shirt.slug}`)

    // The page also lists similar products, each with a price of its own.
    const price = page.getByTestId('price').first()

    await expect(price).toContainText('300,00')
    await page.getByRole('button', { name: 'gök' }).click()
    await expect(price).toContainText('320,00')
    await expect(page.locator('s').first()).toContainText('400,00')

    await page.getByRole('button', { name: 'XL' }).click()
    await expect(price).toContainText('340,00')
    await expect(page.getByTestId('stock-state')).toHaveText('Galmady')
    await expect(page.getByTestId('add-to-cart')).toBeDisabled()
  })

  test('the uploaded picture is shown', async ({ page }) => {
    await open(page, `/products/${shirt.slug}`)

    const image = page.getByTestId('gallery-main').locator('img')

    await expect(image).toBeVisible()
    await expect
      .poll(() => image.evaluate((el: HTMLImageElement) => el.naturalWidth))
      .toBeGreaterThan(0)
  })

  test('the description keeps formatting and loses anything that could run', async ({
    page,
    request,
  }) => {
    const html = await (await request.get(url(`/products/${shirt.slug}`))).text()

    expect(html).toContain('<strong>köýnek</strong>')
    // Text that merely mentions it may appear, escaped, in the product lists on the same page.
    expect(html).not.toContain('<script>window.hacked')
    expect(html).not.toContain('onerror=')

    await open(page, `/products/${shirt.slug}`)
    await expect(page.getByTestId('product-description').locator('strong')).toHaveText('köýnek')
    expect(
      await page.evaluate(() => (window as unknown as { hacked?: boolean }).hacked),
    ).toBeUndefined()
  })
})

test.describe('on a phone', () => {
  test.use({ viewport: { width: 390, height: 780 } })

  for (const path of [
    '/',
    '/catalog',
    '/catalog/telefonlar',
    '/products/galaxy-a55',
    '/search?q=galaxy',
    '/cart',
  ]) {
    test(`${path} does not scroll sideways`, async ({ page }) => {
      await open(page, path)
      await page.waitForLoadState('networkidle')

      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
      )

      expect(overflow).toBeLessThanOrEqual(0)
    })
  }

  test('the bottom menu reaches the main pages', async ({ page }) => {
    await open(page, '/')

    const nav = page.getByTestId('bottom-nav')

    await nav.getByRole('link', { name: 'Katalog' }).click()
    await expect(page).toHaveURL(/\/catalog$/)
    await nav.getByRole('link', { name: 'Sebet' }).click()
    await expect(page).toHaveURL(/\/cart$/)
  })
})
