import { randomBytes } from 'node:crypto'
import {
  expect,
  request,
  type APIRequestContext,
  type APIResponse,
  type Browser,
  type BrowserContext,
  type Page,
} from '@playwright/test'
import { setUpTwoFactor, signIn } from './admin'
import { API_URL, createConfirmedUser, linkFromMail, PASSWORD } from './stack'

/**
 * The page keeps its token in memory, out of reach of scripts. Tests that prepare data through the
 * API borrow it from the page's own requests, which saves a second sign-in and a used TOTP code.
 */
export function watchToken(page: Page): { current: () => string } {
  let token = ''

  page.on('request', (sent) => {
    const header = sent.headers().authorization

    if (header && sent.url().startsWith(API_URL)) {
      token = header
    }
  })

  return {
    current: () => {
      expect(token, 'the page has not called the API yet').not.toBe('')

      return token
    },
  }
}

export async function ok<T = unknown>(response: APIResponse): Promise<T> {
  expect(
    response.ok(),
    `${response.url()} answered ${response.status()}: ${await response.text()}`,
  ).toBe(true)

  const text = await response.text()

  return (text ? JSON.parse(text) : undefined) as T
}

export function adminApi(authorization: string): Promise<APIRequestContext> {
  return request.newContext({
    baseURL: API_URL,
    extraHTTPHeaders: { Authorization: authorization, 'X-Requested-With': 'ComTest' },
  })
}

export const unique = (prefix: string) => `${prefix}-${randomBytes(4).toString('hex')}`

export interface SeededProduct {
  id: string
  variantId: string
  sku: string
  name: string
  version: number
}

/** A published product with one variant, the shape the storefront can sell. */
export async function createProduct(
  api: APIRequestContext,
  options: { name?: string; publish?: boolean; price?: number } = {},
): Promise<SeededProduct> {
  const slug = unique('e2e-product')
  const sku = unique('SKU').toUpperCase()
  const name = options.name ?? slug
  const product = await ok<{ id: string; variants: { id: string }[]; version: number }>(
    await api.post('/api/v1/admin/catalog/products', {
      data: {
        slug,
        translations: [{ language: 'tk', name }],
        variants: [
          { id: null, sku, price: options.price ?? 120.5, currency: 'TMT', isActive: true },
        ],
      },
    }),
  )
  let version = product.version

  if (options.publish !== false) {
    const published = await ok<{ version: number }>(
      await api.put(`/api/v1/admin/catalog/products/${product.id}/status`, {
        data: { status: 'published' },
      }),
    )

    version = published.version
  }

  return { id: product.id, variantId: product.variants[0]!.id, sku, name, version }
}

export interface SeededStore {
  warehouseId: string
  shippingMethodId: string
  product: SeededProduct
}

/** What a store needs before anyone can buy from it. */
export async function seedSellableStore(
  api: APIRequestContext,
  options: { stock?: number } = {},
): Promise<SeededStore> {
  const warehouse = await ok<{ id: string }>(
    await api.post('/api/v1/admin/inventory/warehouses', {
      data: { name: 'Esasy', code: 'MAIN', isDefault: true, isActive: true },
    }),
  )
  const zone = await ok<{ id: string }>(
    await api.post('/api/v1/admin/shipping/zones', {
      data: { name: 'Aşgabat', regions: ['ashgabat'], isActive: true },
    }),
  )
  const method = await ok<{ id: string }>(
    await api.post('/api/v1/admin/shipping/methods', {
      data: {
        type: 'courier',
        names: { tk: 'Kurýer' },
        price: 10,
        currency: 'TMT',
        zoneId: zone.id,
        isActive: true,
      },
    }),
  )
  const product = await createProduct(api)

  await ok(
    await api.post('/api/v1/admin/inventory/stock/adjustments', {
      data: {
        warehouseId: warehouse.id,
        variantId: product.variantId,
        delta: options.stock ?? 50,
        type: 'receipt',
        reason: 'E2E',
      },
    }),
  )

  return { warehouseId: warehouse.id, shippingMethodId: method.id, product }
}

const CHECKOUT_RETRIES = 30
const FALLBACK_RETRY_SECONDS = 5

export interface PlacedOrder {
  id: string
  number: string
  phone: string
}

/** Buys through the public checkout, the way a guest in the storefront does. */
export async function placeGuestOrder(
  slug: string,
  store: { variantId: string; shippingMethodId: string; phone?: string; name?: string },
): Promise<PlacedOrder> {
  const phone =
    store.phone ?? `+9936${String(Math.floor(Math.random() * 9_999_999)).padStart(7, '0')}`
  const guest = await request.newContext({
    baseURL: API_URL,
    extraHTTPHeaders: { 'X-Tenant': slug, 'X-Requested-With': 'ComTest' },
  })

  await ok(
    await guest.post('/api/v1/cart/items', { data: { variantId: store.variantId, quantity: 1 } }),
  )

  // Guests share one limit per address, and the storefront's tests spend the same allowance.
  const checkout = () =>
    guest.post('/api/v1/checkout', {
      headers: { 'Idempotency-Key': key },
      data: {
        shippingMethodId: store.shippingMethodId,
        paymentMethod: 'cashOnDelivery',
        contact: { name: store.name ?? 'Aýna Myratowa', phone, email: 'ayna@example.com' },
        address: {
          recipient: store.name ?? 'Aýna Myratowa',
          phone,
          region: 'ashgabat',
          district: null,
          city: 'Aşgabat',
          line1: 'Magtymguly şaýoly 1',
          line2: null,
          postalCode: null,
        },
      },
    })

  const key = unique('e2e-order')
  let answer = await checkout()

  for (let attempt = 0; answer.status() === 429 && attempt < CHECKOUT_RETRIES; attempt += 1) {
    const wait = Number(answer.headers()['retry-after']) || FALLBACK_RETRY_SECONDS

    await new Promise((resolve) => setTimeout(resolve, (wait + Math.random()) * 1000))
    answer = await checkout()
  }

  const placed = await ok<{ order: { id: string; number: string } }>(answer)

  await guest.dispose()

  return { id: placed.order.id, number: placed.order.number, phone }
}

/** A small real image. The server decodes uploads, so random bytes would be turned away. */
export async function realPng(page: Page): Promise<Buffer> {
  const dataUrl = await page.evaluate(() => {
    const canvas = document.createElement('canvas')

    canvas.width = 320
    canvas.height = 240

    const context = canvas.getContext('2d')!

    context.fillStyle = '#145b45'
    context.fillRect(0, 0, 320, 240)
    context.fillStyle = '#fae3dd'
    context.fillRect(40, 40, 120, 120)

    return canvas.toDataURL('image/png')
  })

  return Buffer.from(dataUrl.split(',')[1]!, 'base64')
}

/** Fails when the page itself scrolls sideways. Tables may scroll inside their own box. */
export async function expectNoSidewaysScroll(page: Page): Promise<void> {
  const overflow = await page.evaluate(() => {
    const root = document.documentElement

    return { scroll: root.scrollWidth, client: root.clientWidth }
  })

  expect(overflow.scroll, 'the page scrolls sideways').toBeLessThanOrEqual(overflow.client)
}

/** Opens a page from the side menu, which sits behind a button on phones. */
export async function openFromMenu(page: Page, name: string): Promise<void> {
  const menuButton = page.getByTestId('menu-button')

  if (await menuButton.isVisible()) {
    await menuButton.click()
  }

  await page.locator(`[data-testid="nav-${name}"]:visible`).click()
}

/** Chooses from an Element Plus select by its visible text. */
export async function choose(page: Page, testId: string, option: string): Promise<void> {
  await page.getByTestId(testId).click()
  await page.getByRole('option', { name: option, exact: true }).click()
}

export async function chooseTab(page: Page, name: string): Promise<void> {
  await page.getByRole('tab', { name, exact: true }).click()
}

/** The one text editor on screen. The other languages' editors stay mounted but hidden. */
export function visibleEditor(page: Page) {
  return page.locator('[data-testid="rich-text"]:visible')
}

/** Says yes to an invitation the way the accept page will, as an account that already exists. */
export async function acceptInvitation(email: string): Promise<void> {
  const link = await linkFromMail(email, 'accept-invitation')
  const token = new URLSearchParams(link.split('#')[1]).get('token')!
  const context = await request.newContext({
    baseURL: API_URL,
    extraHTTPHeaders: { 'X-Requested-With': 'ComTest' },
  })
  const login = await ok<{ accessToken: string }>(
    await context.post('/api/v1/auth/login', { data: { login: email, password: PASSWORD } }),
  )

  await ok(
    await context.post('/api/v1/tenants/invitations/accept', {
      headers: { Authorization: `Bearer ${login.accessToken}` },
      data: { token },
    }),
  )
  await context.dispose()
}

/** A colleague in another browser, invited by API and signed in past the forced two-factor. */
export async function addStaffMember(
  api: APIRequestContext,
  browser: Browser,
  role: 'Manager' | 'Staff',
): Promise<{ page: Page; context: BrowserContext; email: string }> {
  const email = await createConfirmedUser(`E2E ${role}`)

  await ok(await api.post('/api/v1/admin/staff/invitations', { data: { email, role } }))
  await acceptInvitation(email)

  const context = await browser.newContext()
  const page = await context.newPage()

  await signIn(page, email)
  await setUpTwoFactor(page)
  await expect(page.getByRole('heading', { name: /Hoş geldiňiz/ })).toBeVisible()

  return { page, context, email }
}

/** Buys as a signed-in customer, who has an account and a limit of their own. */
export async function placeCustomerOrder(
  slug: string,
  email: string,
  store: { variantId: string; shippingMethodId: string },
): Promise<PlacedOrder> {
  const phone = `+9936${String(Math.floor(Math.random() * 9_999_999)).padStart(7, '0')}`
  const anonymous = await request.newContext({
    baseURL: API_URL,
    extraHTTPHeaders: { 'X-Requested-With': 'ComTest' },
  })
  const { accessToken } = await ok<{ accessToken: string }>(
    await anonymous.post('/api/v1/auth/login', { data: { login: email, password: PASSWORD } }),
  )

  await anonymous.dispose()

  const customer = await request.newContext({
    baseURL: API_URL,
    extraHTTPHeaders: {
      Authorization: `Bearer ${accessToken}`,
      'X-Tenant': slug,
      'X-Requested-With': 'ComTest',
    },
  })

  await ok(
    await customer.post('/api/v1/cart/items', {
      data: { variantId: store.variantId, quantity: 1 },
    }),
  )

  const placed = await ok<{ order: { id: string; number: string } }>(
    await customer.post('/api/v1/checkout', {
      headers: { 'Idempotency-Key': unique('e2e-order') },
      data: {
        shippingMethodId: store.shippingMethodId,
        paymentMethod: 'cashOnDelivery',
        contact: { name: 'Hasaply Müşderi', phone, email },
        address: {
          recipient: 'Hasaply Müşderi',
          phone,
          region: 'ashgabat',
          district: null,
          city: 'Aşgabat',
          line1: 'Bitarap Türkmenistan şaýoly 5',
          line2: null,
          postalCode: null,
        },
      },
    }),
  )

  await customer.dispose()

  return { id: placed.order.id, number: placed.order.number, phone }
}

/**
 * Makes the page believe its user lacks a permission, while the API keeps seeing the real token.
 * No role lacks `customers.read` today, and this is how to see the panel as if one did. It says
 * nothing about the server, which is why tests of the server's own checks use real roles instead.
 */
export async function hidePermissionFromPage(page: Page, permission: string): Promise<void> {
  let realToken = ''

  await page.route('**/api/v1/**', async (route) => {
    const headers = route.request().headers()

    if (realToken && headers.authorization) {
      headers.authorization = `Bearer ${realToken}`
    }

    await route.continue({ headers })
  })
  await page.route('**/api/v1/auth/refresh', async (route) => {
    const response = await route.fetch()
    const body = (await response.json()) as { accessToken?: string }

    if (!body.accessToken) {
      await route.fulfill({ response })

      return
    }

    realToken = body.accessToken

    const [head, payload, signature] = realToken.split('.') as [string, string, string]
    const claims = JSON.parse(Buffer.from(payload, 'base64url').toString()) as {
      permission: string[]
    }

    claims.permission = claims.permission.filter((item) => item !== permission)

    const forged = Buffer.from(JSON.stringify(claims)).toString('base64url')

    await route.fulfill({
      response,
      json: { ...body, accessToken: `${head}.${forged}.${signature}` },
    })
  })
}
