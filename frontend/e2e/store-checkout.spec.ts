import type { Page } from '@playwright/test'
import { expect, test } from './support/fixtures'
import {
  archiveProduct,
  changePrice,
  createProduct,
  createPromotion,
  type CreatedProduct,
} from './support/demo-store'
import { createConfirmedUser, PASSWORD, STOREFRONT_URL } from './support/stack'

const url = (path: string) => `${STOREFRONT_URL}${path}`

/** Waits for the JavaScript as well, since clicks on server-rendered HTML go nowhere until it runs. */
const open = (page: Page, path: string) => page.goto(url(path), { waitUntil: 'networkidle' })

async function addToCart(page: Page, product: CreatedProduct): Promise<void> {
  await open(page, `/products/${product.slug}`)
  await page.getByTestId('add-to-cart').click()
  await expect(page.getByTestId('cart-count').first()).toBeVisible()
}

async function fillContact(page: Page, name = 'Aýna Meredowa'): Promise<void> {
  await page.getByTestId('contact-name').fill(name)
  await page.getByTestId('contact-phone').fill(`+9936${String(Date.now()).slice(-7)}`)
  await page.getByTestId('next-step').click()
}

async function chooseRegion(page: Page, region: string): Promise<void> {
  await page.getByTestId('address-region').click()
  await page.getByRole('option', { name: region }).click()
}

async function fillAddress(page: Page, region = 'Mary'): Promise<void> {
  await chooseRegion(page, region)
  await page.getByTestId('address-line1').fill('Magtymguly köçesi, 12')
  await page.getByTestId('next-step').click()
}

/** From the address step through shipping and payment to the review. */
async function chooseDeliveryAndPayment(page: Page, shipping: RegExp): Promise<void> {
  await page.getByTestId('shipping-option').filter({ hasText: shipping }).click()
  await page.getByTestId('next-step').click()
  await page.getByTestId('payment-option').first().click()
  await page.getByTestId('next-step').click()
}

test('a guest buys a product from start to finish', async ({ page }) => {
  const product = await createProduct({ variants: [{ price: 250, stock: 10 }] })

  await addToCart(page, product)
  await expect(page.getByTestId('cart-drawer').getByTestId('cart-line')).toContainText(product.name)
  await page.getByTestId('drawer-cart').click()

  await expect(page).toHaveURL(/\/cart$/)
  await expect(page.getByTestId('cart-subtotal')).toContainText('250,00')
  await page.getByTestId('go-checkout').click()

  await fillContact(page)
  await fillAddress(page, 'Mary')

  // The delivery price comes from the server: 50 for the regions in the demo data.
  await page.getByTestId('shipping-option').filter({ hasText: 'Welaýat kurýeri' }).click()
  await expect(page.getByTestId('total-shipping')).toContainText('50,00')
  await expect(page.getByTestId('total')).toContainText('300,00')
  await page.getByTestId('next-step').click()
  await page.getByTestId('payment-option').first().click()
  await page.getByTestId('next-step').click()

  await page.getByTestId('place-order').click()

  await expect(page).toHaveURL(/\/order-confirmed/)
  await expect(page.getByTestId('order-confirmed')).toBeVisible()
  await expect(page.getByTestId('order-number')).not.toBeEmpty()
  await expect(page.getByTestId('order-item')).toContainText(product.name)
  await expect(page.getByTestId('total-shipping')).toContainText('50,00')
  await expect(page.getByTestId('total')).toContainText('300,00')

  // A guest's only way back to the order is this link, which has to keep working.
  await page.getByTestId('track-link').click()
  await expect(page).toHaveURL(/\/track#token=/)
  await page.reload()
  await expect(page.getByTestId('order-item')).toContainText(product.name)

  // The cart is empty afterwards.
  await open(page, '/cart')
  await expect(page.getByText('Sebediňiz boş')).toBeVisible()
})

test('a promo code lowers the total on screen and on the order', async ({ page }, testInfo) => {
  const code = `E2E-${testInfo.project.name}-${Date.now()}`.toUpperCase().slice(0, 32)
  const promotion = await createPromotion({ code, value: 10 })
  const product = await createProduct({ variants: [{ price: 200, stock: 10 }] })

  await addToCart(page, product)
  await open(page, '/checkout')
  await fillContact(page)
  await fillAddress(page, 'Mary')

  const input = page.getByTestId('promo-input')

  // A wrong code changes nothing and says why.
  await input.fill('HIC-BOLMADYK')
  await page.getByTestId('promo-apply').click()
  await expect(page.getByTestId('promo-error')).toHaveText('Bu promokod nädogry.')
  await expect(page.getByTestId('promo-applied')).toHaveCount(0)

  await input.fill(code.toLowerCase())
  await page.getByTestId('promo-apply').click()
  await expect(page.getByTestId('promo-applied')).toContainText(`E2E ${code}`)
  await expect(page.getByTestId('promo-error')).toHaveCount(0)
  await expect(page.getByTestId('checkout-summary')).toContainText('20,00')

  // Removing it brings the full price back, and a code can go on again after that.
  await page.getByTestId('promo-remove').click()
  await expect(page.getByTestId('promo-applied')).toHaveCount(0)
  await input.fill(code)
  await page.getByTestId('promo-apply').click()
  await expect(page.getByTestId('promo-applied')).toBeVisible()

  await chooseDeliveryAndPayment(page, /Welaýat kurýeri/)
  await expect(page.getByTestId('total')).toContainText('230,00')
  await page.getByTestId('place-order').click()

  await expect(page).toHaveURL(/\/order-confirmed/)
  await expect(page.getByTestId('total')).toContainText('230,00')

  await promotion.remove()
})

test('picking up in the store needs no address', async ({ page }) => {
  const product = await createProduct({ variants: [{ price: 90, stock: 10 }] })

  await addToCart(page, product)
  await open(page, '/checkout')
  await fillContact(page)

  await page.getByTestId('delivery-kind').getByText('Özüm almak').click()
  await expect(page.getByTestId('address-line1')).toHaveCount(0)
  await page.getByTestId('next-step').click()

  await chooseDeliveryAndPayment(page, /Dükandan almak/)
  await page.getByTestId('place-order').click()

  await expect(page.getByTestId('order-confirmed')).toBeVisible()
  await expect(page.getByTestId('total')).toContainText('90,00')
})

test('a double click on the order button creates one order', async ({ page }) => {
  const product = await createProduct({ variants: [{ price: 120, stock: 10 }] })
  let posts = 0

  page.on('request', (request) => {
    if (request.method() === 'POST' && new URL(request.url()).pathname === '/api/v1/checkout') {
      posts += 1
    }
  })

  await addToCart(page, product)
  await open(page, '/checkout')
  await fillContact(page)
  await fillAddress(page)
  await chooseDeliveryAndPayment(page, /Welaýat kurýeri/)

  await page.getByTestId('place-order').dblclick()

  await expect(page.getByTestId('order-confirmed')).toBeVisible()
  expect(posts).toBe(1)
})

test('a lost answer can be retried without a second order', async ({ page }) => {
  const product = await createProduct({ variants: [{ price: 140, stock: 10 }] })
  const keys: (string | null)[] = []
  let dropped = false

  // The order reaches the server, and only the answer disappears on the way back.
  await page.route('**/api/v1/checkout', async (route) => {
    keys.push(route.request().headers()['idempotency-key'] ?? null)

    if (!dropped) {
      dropped = true
      await route.fetch()
      await route.abort('connectionfailed')

      return
    }

    await route.continue()
  })

  await addToCart(page, product)
  await open(page, '/checkout')
  await fillContact(page)
  await fillAddress(page)
  await chooseDeliveryAndPayment(page, /Welaýat kurýeri/)

  await page.getByTestId('place-order').click()
  await expect(page.getByTestId('place-error')).toContainText('baglanyşyk')

  await page.getByTestId('place-order').click()
  await expect(page.getByTestId('order-confirmed')).toBeVisible()

  expect(keys).toHaveLength(2)
  expect(keys[0]).toMatch(/^[A-Za-z0-9_-]{8,64}$/)
  expect(keys[1]).toBe(keys[0])
})

test('the total on screen is the one the server sent', async ({ page }) => {
  const product = await createProduct({ variants: [{ price: 111.11, stock: 10 }] })
  const totals: number[] = []

  page.on('response', async (response) => {
    if (response.url().endsWith('/checkout/quote') && response.ok()) {
      totals.push(((await response.json()) as { totals: { total: number } }).totals.total)
    }
  })

  await addToCart(page, product)
  await open(page, '/checkout')
  await fillContact(page)
  await fillAddress(page)
  await page.getByTestId('shipping-option').filter({ hasText: 'Welaýat kurýeri' }).click()

  await expect.poll(() => totals.at(-1)).toBe(161.11)
  await expect(page.getByTestId('total')).toContainText('161,11')
})

test('a price change just before ordering stops the order and shows the new total', async ({
  page,
}) => {
  const product = await createProduct({ variants: [{ price: 200, stock: 10 }] })

  await addToCart(page, product)
  await open(page, '/checkout')
  await fillContact(page)
  await fillAddress(page)
  await chooseDeliveryAndPayment(page, /Welaýat kurýeri/)
  await expect(page.getByTestId('total')).toContainText('250,00')

  await changePrice(product, 230)
  await page.getByTestId('place-order').click()

  await expect(page.getByTestId('place-error')).toBeVisible()
  await expect(page).toHaveURL(/\/checkout$/)
  await expect(page.getByTestId('total')).toContainText('280,00')

  await page.getByTestId('place-order').click()
  await expect(page.getByTestId('order-confirmed')).toBeVisible()
})

test('payment instructions from the order are shown once, on its own confirmation', async ({
  page,
}) => {
  const product = await createProduct({ variants: [{ price: 80, stock: 5 }] })

  // Only bank transfer has instructions, which the demo store does not have switched on.
  await page.route('**/api/v1/checkout', async (route) => {
    const response = await route.fetch()
    const body = (await response.json()) as Record<string, unknown>

    await route.fulfill({
      response,
      json: { ...body, paymentInstructions: 'Hasap: 1234, sargyt belgisini ýazyň' },
    })
  })

  await addToCart(page, product)
  await open(page, '/checkout')
  await fillContact(page)
  await fillAddress(page)
  await chooseDeliveryAndPayment(page, /Welaýat kurýeri/)
  await page.getByTestId('place-order').click()

  await expect(page.getByTestId('payment-instructions')).toContainText('Hasap: 1234')

  await page.reload()
  await expect(page.getByTestId('order-confirmed')).toBeVisible()
  await expect(page.getByTestId('payment-instructions')).toHaveCount(0)
})

test('a forged confirmation address shows no order', async ({ page }) => {
  await open(page, '/order-confirmed#token=not-a-real-token')

  await expect(page.getByText('Sargyt tapylmady')).toBeVisible()
  await expect(page.getByTestId('order-confirmed')).toHaveCount(0)
})

test.describe('the cart warns about changes', () => {
  test('when the price moves', async ({ page }) => {
    const product = await createProduct({ variants: [{ price: 500, stock: 5 }] })

    await addToCart(page, product)
    await changePrice(product, 450)
    await open(page, '/cart')

    await expect(page.getByTestId('price-changed')).toContainText('500,00')
    await expect(page.getByTestId('price-changed')).toContainText('450,00')
    await expect(page.getByTestId('cart-subtotal')).toContainText('450,00')
  })

  test('when more is wanted than the warehouse holds', async ({ page }) => {
    const product = await createProduct({ variants: [{ price: 70, stock: 2 }] })

    await open(page, `/products/${product.slug}`)
    await page.locator('.el-input-number input').fill('2')
    await page.getByTestId('add-to-cart').click()
    await expect(page.getByTestId('cart-count').first()).toHaveText('2')
    await open(page, '/cart')

    await page.getByTestId('cart-line').locator('.el-input-number__increase').click()

    await expect(page.getByTestId('line-problem')).toContainText('ýeterlik')
    await expect(page.getByTestId('go-checkout')).toBeDisabled()
  })

  test('when a product stops being sold', async ({ page }) => {
    const product = await createProduct({ variants: [{ price: 60, stock: 5 }] })

    await addToCart(page, product)
    await archiveProduct(product)
    await open(page, '/cart')

    await expect(page.getByTestId('line-problem')).toBeVisible()
    await expect(page.getByTestId('cart-problems')).toBeVisible()
    await expect(page.getByTestId('go-checkout')).toBeDisabled()
  })
})

test('changing quantities and removing lines follows the server', async ({ page }) => {
  const product = await createProduct({ variants: [{ price: 10.5, stock: 20 }] })

  await addToCart(page, product)
  await open(page, '/cart')
  await page.getByTestId('cart-line').locator('.el-input-number__increase').click()

  await expect(page.getByTestId('cart-subtotal')).toContainText('21,00')

  await page.getByTestId('remove-line').click()
  await expect(page.getByText('Sebediňiz boş')).toBeVisible()
})

test.describe('with an account', () => {
  async function signIn(page: Page, email: string, returnUrl = '/account'): Promise<void> {
    await open(page, `/login?returnUrl=${encodeURIComponent(returnUrl)}`)
    await page.getByLabel('E-poçta ýa-da telefon').fill(email)
    await page.locator('input[type="password"]').fill(PASSWORD)
    await page.getByRole('button', { name: 'Girmek', exact: true }).click()
    await expect(page).toHaveURL(url(returnUrl))
  }

  test("a guest's cart joins the account on signing in", async ({ page }) => {
    const email = await createConfirmedUser()
    const product = await createProduct({ variants: [{ price: 33, stock: 5 }] })

    await addToCart(page, product)
    await signIn(page, email, '/cart')

    await expect(page.getByTestId('cart-line')).toContainText(product.name)
    await expect(page.getByTestId('cart-subtotal')).toContainText('33,00')
  })

  test('a saved address is offered at checkout, and the order lands in the account', async ({
    page,
  }) => {
    const email = await createConfirmedUser('Merdan Ataýew')
    const product = await createProduct({ variants: [{ price: 75, stock: 5 }] })

    await signIn(page, email, '/account/addresses')
    await page.getByTestId('add-address').click()

    const dialog = page.getByRole('dialog')

    await dialog.getByTestId('address-recipient').fill('Merdan Ataýew')
    await dialog.getByTestId('address-phone').fill(`+9936${String(Date.now()).slice(-7)}`)
    await chooseRegion(page, 'Aşgabat')
    await dialog.getByTestId('address-line1').fill('Bitarap köçesi, 5')
    await dialog.getByRole('button', { name: 'Sakla' }).click()
    await expect(page.getByTestId('address-card')).toContainText('Bitarap köçesi, 5')
    await expect(page.getByTestId('address-card')).toContainText('Esasy')

    await addToCart(page, product)
    await open(page, '/checkout')
    await expect(page.getByTestId('contact-name')).toHaveValue('Merdan Ataýew')
    await page.getByTestId('contact-phone').fill(`+9936${String(Date.now()).slice(-7)}`)
    await page.getByTestId('next-step').click()

    await expect(page.getByTestId('saved-address')).toContainText('Bitarap köçesi')
    await page.getByTestId('next-step').click()
    await chooseDeliveryAndPayment(page, /Kurýer/)
    await page.getByTestId('place-order').click()
    await expect(page.getByTestId('order-confirmed')).toBeVisible()

    const number = await page.getByTestId('order-number').innerText()

    await open(page, '/account/orders')
    await expect(page.getByTestId('order-row')).toContainText(number)
    await page.getByTestId('order-row').click()

    await expect(page.getByTestId('order-number')).toHaveText(number)
    await expect(page.getByTestId('order-status')).toContainText('Garaşylýar')

    await page.getByTestId('cancel-order').click()
    await page.getByRole('dialog').getByRole('button', { name: 'Hawa, ýatyr' }).click()
    await expect(page.getByTestId('order-status')).toContainText('Ýatyryldy')
    await expect(page.getByTestId('cancel-order')).toHaveCount(0)
  })

  test('another customer cannot open my order', async ({ page, browser }) => {
    const email = await createConfirmedUser()
    const product = await createProduct({ variants: [{ price: 45, stock: 5 }] })

    await signIn(page, email)
    await addToCart(page, product)
    await open(page, '/checkout')
    await page.getByTestId('contact-phone').fill(`+9936${String(Date.now()).slice(-7)}`)
    await page.getByTestId('next-step').click()
    await page.getByTestId('delivery-kind').getByText('Özüm almak').click()
    await page.getByTestId('next-step').click()
    await chooseDeliveryAndPayment(page, /Dükandan almak/)
    await page.getByTestId('place-order').click()
    await expect(page.getByTestId('order-confirmed')).toBeVisible()
    await open(page, '/account/orders')
    await page.getByTestId('order-row').click()
    await expect(page.getByTestId('order-number')).toBeVisible()

    const orderPath = new URL(page.url()).pathname
    const other = await browser.newContext({ locale: 'tk-TM' })
    const otherPage = await other.newPage()

    await signIn(otherPage, await createConfirmedUser())
    await open(otherPage, orderPath)

    await expect(otherPage.getByTestId('order-number')).toHaveCount(0)
    await expect(otherPage.getByRole('alert')).toBeVisible()
    await other.close()
  })

  test('addresses can be edited and deleted, with a confirmation', async ({ page }) => {
    const email = await createConfirmedUser()

    await signIn(page, email, '/account/addresses')
    await page.getByTestId('add-address').click()

    const dialog = page.getByRole('dialog')

    await dialog.getByRole('button', { name: 'Sakla' }).click()
    await expect(dialog.getByText('Bu meýdany dolduryň').first()).toBeVisible()

    await dialog.getByTestId('address-recipient').fill('Aýna')
    await dialog.getByTestId('address-phone').fill(`+9936${String(Date.now()).slice(-7)}`)
    await chooseRegion(page, 'Lebap')
    await dialog.getByTestId('address-line1').fill('Birinji salgy')
    await dialog.getByRole('button', { name: 'Sakla' }).click()
    await expect(page.getByTestId('address-card')).toContainText('Birinji salgy')

    await page.getByTestId('edit-address').click()
    await dialog.getByTestId('address-line1').fill('Ikinji salgy')
    await dialog.getByRole('button', { name: 'Sakla' }).click()
    await expect(page.getByTestId('address-card')).toContainText('Ikinji salgy')

    await page.getByTestId('delete-address').click()
    await expect(page.getByTestId('address-card')).toHaveCount(1)
    await page.getByRole('dialog').getByRole('button', { name: 'Poz' }).click()
    await expect(page.getByTestId('address-card')).toHaveCount(0)
  })
})
