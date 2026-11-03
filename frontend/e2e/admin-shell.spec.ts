import { signInAsNewOwner } from './support/admin'
import {
  adminApi,
  createProduct,
  expectNoSidewaysScroll,
  ok,
  openFromMenu,
  placeGuestOrder,
  seedSellableStore,
  unique,
  watchToken,
} from './support/admin-data'
import { expect, test } from './support/fixtures'
import { ADMIN_URL } from './support/stack'

test('the chart library and the editor load only where they are used', async ({ page }) => {
  await signInAsNewOwner(page, 'Bölek Dükany')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  const loaded: string[] = []

  page.on('response', (response) => loaded.push(response.url()))

  await page.goto(`${ADMIN_URL}/tk/orders`)
  await expect(page.getByRole('heading', { name: 'Sargytlar' })).toBeVisible()
  await page.goto(`${ADMIN_URL}/tk/products/new`)
  await expect(page.getByTestId('product-slug')).toBeVisible()
  expect(loaded.filter((url) => /OrdersChart/.test(url))).toEqual([])

  await page.goto(`${ADMIN_URL}/tk/orders`)
  await expect(page.getByRole('heading', { name: 'Sargytlar' })).toBeVisible()
  loaded.length = 0
  await page.reload()
  await expect(page.getByRole('heading', { name: 'Sargytlar' })).toBeVisible()
  expect(loaded.filter((url) => /RichTextEditor|OrdersChart/.test(url))).toEqual([])
})

test('breadcrumbs and the side menu follow the page', async ({ page }) => {
  test.slow()

  const token = watchToken(page)

  await signInAsNewOwner(page, 'Gezelenç Dükany')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  const api = await adminApi(token.current())
  const product = await createProduct(api, { name: 'Gezelenç harydy' })

  await openFromMenu(page, 'products')
  await expect(page).toHaveURL(/\/tk\/products$/)
  await expect(page.getByTestId('breadcrumbs')).toContainText('Baş sahypa')
  await expect(page.getByTestId('breadcrumbs')).toContainText('Harytlar')
  await expect(page).toHaveTitle(/Harytlar/)

  await page.goto(`${ADMIN_URL}/tk/products/${product.id}`)
  await expect(page.getByTestId('breadcrumbs')).toContainText('Gezelenç harydy')
  await expect(page).toHaveTitle(/Gezelenç harydy/)

  await page.getByTestId('breadcrumbs').getByText('Harytlar').click()
  await expect(page).toHaveURL(/\/tk\/products$/)
  await api.dispose()
})

test('the search palette finds pages, products, orders and customers', async ({ page }) => {
  // An order goes through the public checkout, which has its own low limit per address.
  test.setTimeout(300_000)

  const token = watchToken(page)
  const owner = await signInAsNewOwner(page, 'Gözleg Dükany')

  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  const api = await adminApi(token.current())
  const store = await seedSellableStore(api)
  const product = await createProduct(api, { name: 'Gyzyl köýnek' })
  const draft = await createProduct(api, { name: 'Garalama köýnek', publish: false })
  const order = await placeGuestOrder(owner.slug, {
    variantId: store.product.variantId,
    shippingMethodId: store.shippingMethodId,
    phone: '+99361222333',
    name: 'Gülşat Rejepowa',
  })

  await page.goto(`${ADMIN_URL}/tk`)
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  const input = page.getByTestId('palette-input')
  const results = page.getByTestId('palette-result')

  await page.keyboard.press('Control+k')
  await expect(input).toBeFocused()
  await input.fill('sargyt')
  await expect(results.first()).toContainText('Sargytlar')
  await page.keyboard.press('Enter')
  await expect(page).toHaveURL(/\/tk\/orders$/)

  await page.keyboard.press('Control+k')
  await input.fill('köýnek')
  await expect(results.filter({ hasText: 'Gyzyl köýnek' })).toBeVisible()
  // A draft is not in the public search, and still has to be findable here.
  await expect(results.filter({ hasText: 'Garalama köýnek' })).toBeVisible()
  await results.filter({ hasText: 'Garalama köýnek' }).click()
  await expect(page).toHaveURL(new RegExp(`/products/${draft.id}$`))

  await page.keyboard.press('Control+k')
  await input.fill(order.number)
  await expect(results.filter({ hasText: order.number })).toBeVisible()
  await results.filter({ hasText: order.number }).click()
  await expect(page).toHaveURL(new RegExp(`/orders/${order.id}$`))

  await page.keyboard.press('Control+k')
  await input.fill('+99361222333')

  const customer = results.filter({ hasText: 'Gülşat Rejepowa' })

  await expect(customer.first()).toBeVisible()
  await customer.last().click()
  await expect(page).toHaveURL(/\/customers\/guest\/|\/orders\//)

  // Moving through the results by keyboard, and closing.
  await page.keyboard.press('Control+k')
  await input.fill('a')
  await expect(results.first()).toHaveAttribute('aria-selected', 'true')
  await page.keyboard.press('ArrowDown')
  await expect(results.nth(1)).toHaveAttribute('aria-selected', 'true')
  await page.keyboard.press('Escape')
  await expect(input).toBeHidden()

  expect(product.id).toBeTruthy()
  await api.dispose()
})

test('keyboard shortcuts go places, and stay quiet while typing', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name === 'phone', 'A phone has no such keyboard.')

  await signInAsNewOwner(page, 'Klawiatura Dükany')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  await page.keyboard.press('g')
  await page.keyboard.press('o')
  await expect(page).toHaveURL(/\/tk\/orders$/)

  await page.keyboard.press('g')
  await page.keyboard.press('p')
  await expect(page).toHaveURL(/\/tk\/products$/)

  await page.keyboard.press('Shift+?')
  await expect(page.getByTestId('shortcuts-help')).toBeVisible()
  await page.keyboard.press('Escape')

  await page.keyboard.press('/')
  await expect(page.getByTestId('palette-input')).toBeFocused()
  await page.keyboard.press('Escape')

  await page.goto(`${ADMIN_URL}/tk/orders`)
  await page.getByTestId('filter-search').click()
  await page.keyboard.type('go/?')
  await expect(page).toHaveURL(/\/tk\/orders$/)
  await expect(page.getByTestId('filter-search')).toHaveValue('go/?')
})

test('an owner of two stores can switch, and nothing of the first stays on screen', async ({
  page,
}) => {
  test.slow()

  const token = watchToken(page)
  const first = await signInAsNewOwner(page, 'Birinji Dükan')

  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  const api = await adminApi(token.current())

  await createProduct(api, { name: 'Birinji dükanyň harydy' })

  const secondSlug = unique('e2e-second')

  await ok(
    await api.post('/api/v1/tenants', {
      data: {
        name: 'Ikinji Dükan',
        slug: secondSlug,
        defaultCurrency: 'TMT',
        defaultLanguage: 'tk',
      },
    }),
  )

  await page.reload()

  const menuButton = page.getByTestId('menu-button')
  const openSwitcher = async () => {
    if (await menuButton.isVisible()) {
      await menuButton.click()
    }

    await page.locator('[data-testid="store-switcher"]:visible').click()
  }

  await expect(page.getByRole('heading', { name: /Hoş geldiňiz/ })).toBeVisible()

  const before = await page.locator('[data-testid="store-switcher"]').first().innerText()

  await page.goto(`${ADMIN_URL}/tk/products`)
  await expect(page.getByRole('heading', { name: 'Harytlar' })).toBeVisible()

  const target = before.includes('Birinji') ? secondSlug : first.slug

  await openSwitcher()
  await page.locator(`[data-testid="store-${target}"]:visible`).click()
  await expect(page).toHaveURL(/\/tk$/)
  await expect(page.locator('[data-testid="store-switcher"]').first()).not.toContainText(
    before.trim(),
  )

  await page.goto(`${ADMIN_URL}/tk/products`)

  if (target === secondSlug) {
    await expect(page.getByText('Entek haryt ýok')).toBeVisible()
  } else {
    await expect(page.getByText('Birinji dükanyň harydy')).toBeVisible()
  }

  await api.dispose()
})

test('an idle session warns, can be kept, and otherwise signs out', async ({ page }) => {
  test.slow()
  await page.clock.install()
  await signInAsNewOwner(page, 'Hereketsiz Dükany')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  const warning = page.getByTestId('idle-message')

  await expect(warning).toBeHidden()

  // Thirty minutes minus a minute with no input at all brings the warning up.
  await page.clock.fastForward('29:30')
  await expect(warning).toBeVisible()
  await page.getByTestId('idle-stay').click()
  await expect(warning).toBeHidden()

  // The clock started over, so ten more minutes are still nothing.
  await page.clock.fastForward('10:00')
  await expect(page).not.toHaveURL(/\/login/)
  await expect(warning).toBeHidden()

  await page.clock.fastForward('19:30')
  await expect(warning).toBeVisible()
  await page.clock.fastForward('01:00')
  await expect(page).toHaveURL(/\/login/)
  await expect(page.getByText('Uzak wagt işjeň bolmanyňyz üçin')).toBeVisible()

  await page.goto(`${ADMIN_URL}/tk/products`)
  await expect(page).toHaveURL(/\/login/)
})

test('an unsaved form does not stop an idle sign-out', async ({ page }) => {
  test.slow()
  await page.clock.install()
  await signInAsNewOwner(page, 'Hereketsiz Forma Dükany')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()
  await page.goto(`${ADMIN_URL}/tk/products/new`)
  await page.getByTestId('product-slug').fill('yitip-giden')

  await page.clock.fastForward('31:00')
  await expect(page).toHaveURL(/\/login/)
  await expect(page.getByText('Saklanmadyk üýtgeşmeler')).toHaveCount(0)
})

test('a phone gets the menu in a drawer, and no page scrolls sideways', async ({
  page,
}, testInfo) => {
  test.skip(testInfo.project.name !== 'phone', 'Only phones have the drawer.')

  await signInAsNewOwner(page, 'Telefon Dükany')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()
  await expectNoSidewaysScroll(page)

  await expect(page.locator('[data-testid="side-menu"]:visible')).toHaveCount(0)
  await page.getByTestId('menu-button').click()
  await expect(page.locator('[data-testid="side-menu"]:visible')).toHaveCount(1)
  await page.locator('[data-testid="nav-orders"]:visible').click()
  await expect(page).toHaveURL(/\/tk\/orders$/)
  await expect(page.locator('[data-testid="side-menu"]:visible')).toHaveCount(0)
  await expectNoSidewaysScroll(page)
})
