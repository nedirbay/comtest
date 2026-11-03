import type { Page } from '@playwright/test'
import { signInAsNewOwner } from './support/admin'
import {
  addStaffMember,
  adminApi,
  choose,
  hidePermissionFromPage,
  ok,
  placeCustomerOrder,
  placeGuestOrder,
  seedSellableStore,
  watchToken,
} from './support/admin-data'
import { expect, test } from './support/fixtures'
import { ADMIN_URL, createConfirmedUser } from './support/stack'

// Guests may place only ten orders a minute, across every test running against this stack, so a
// test here can spend a while waiting its turn.
test.describe.configure({ timeout: 300_000 })

async function storeWithOrder(page: Page, name: string) {
  const token = watchToken(page)
  const owner = await signInAsNewOwner(page, name)

  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  const api = await adminApi(token.current())
  const store = await seedSellableStore(api)
  const place = (extra: { phone?: string; name?: string } = {}) =>
    placeGuestOrder(owner.slug, {
      variantId: store.product.variantId,
      shippingMethodId: store.shippingMethodId,
      ...extra,
    })

  return { api, owner, store, place }
}

test('an order moves through its statuses, with tracking and a payment', async ({ page }) => {
  const { api, place } = await storeWithOrder(page, 'Sargyt Dükany')
  const order = await place()

  await page.goto(`${ADMIN_URL}/tk/orders`)
  await page.getByTestId('order-link').first().click()
  await expect(page).toHaveURL(new RegExp(`/orders/${order.id}$`))
  await expect(page.getByRole('heading', { name: `Sargyt ${order.number}` })).toBeVisible()

  const status = page.getByTestId('order-status')

  await expect(status).toHaveText('Garaşylýar')
  // Only the moves the workflow allows from here are offered.
  await expect(page.getByTestId('order-action-shipped')).toHaveCount(0)

  await page.getByTestId('order-action-confirmed').click()
  await page.getByTestId('status-note').fill('Jaň edildi')
  await page.getByTestId('drawer-submit').click()
  await expect(status).toHaveText('Tassyklandy')
  await expect(page.getByTestId('order-history')).toContainText('Jaň edildi')

  await page.getByTestId('order-action-packed').click()
  await page.getByTestId('drawer-submit').click()
  await expect(status).toHaveText('Gaplandy')

  await page.getByTestId('order-action-shipped').click()
  await page.getByTestId('status-carrier').fill('Kurýer hyzmaty')
  await page.getByTestId('status-tracking-number').fill('TRK-123')
  await page.getByTestId('status-tracking-url').fill('https://track.example.com/TRK-123')
  await page.getByTestId('drawer-submit').click()
  await expect(status).toHaveText('Ýolda')
  await expect(page.getByTestId('order-tracking')).toContainText('Kurýer hyzmaty · TRK-123')
  await expect(page.getByTestId('order-tracking-link')).toHaveAttribute(
    'href',
    'https://track.example.com/TRK-123',
  )

  // Payment is its own workflow next to the order's.
  await expect(page.getByTestId('order-payment-state')).toHaveText('Tölenmedik')
  await page.getByTestId('payment-confirm').click()
  await page.getByTestId('payment-reference').fill('KASSA-77')
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByTestId('payment-status')).toHaveText('Tölendi')
  await expect(page.getByTestId('order-payment-state')).toHaveText('Tölendi')

  // A note stays on the order.
  await page.getByTestId('note-input').fill('Müşderi agşam jaň eder')
  await page.getByTestId('note-add').click()
  await expect(page.getByTestId('order-note')).toContainText('Müşderi agşam jaň eder')

  // A reload shows what the server holds, not what the page remembers.
  await page.reload()
  await expect(status).toHaveText('Ýolda')
  await expect(page.getByTestId('payment-status')).toHaveText('Tölendi')
  await expect(page.getByTestId('order-note')).toHaveCount(1)
  await api.dispose()
})

test('a tracking address that could run code is not offered as a link', async ({ page }) => {
  const { api, place } = await storeWithOrder(page, 'Yzarlaýyş Dükany')
  const order = await place()

  await page.goto(`${ADMIN_URL}/tk/orders/${order.id}`)
  await page.getByTestId('order-action-confirmed').click()
  await page.getByTestId('drawer-submit').click()
  await page.getByTestId('order-action-packed').click()
  await page.getByTestId('drawer-submit').click()
  await page.getByTestId('order-action-shipped').click()
  await page.getByTestId('status-tracking-url').fill('javascript:alert(1)')
  await page.getByTestId('drawer-submit').click()
  // The form refuses it, and so does the server if anyone gets past the form.
  await expect(page.getByText('http:// ýa-da https://')).toBeVisible()
  await expect(page.getByTestId('order-status')).toHaveText('Gaplandy')
  await api.dispose()
})

test('cancelling and refunding ask first, and a refund reaches the payment', async ({ page }) => {
  const { api, place } = await storeWithOrder(page, 'Gaýtarma Dükany')
  const paid = await place()
  const dialog = page.getByRole('dialog').filter({ hasText: 'yzyna alyp bolmaýar' })

  await page.goto(`${ADMIN_URL}/tk/orders/${paid.id}`)
  await page.getByTestId('payment-confirm').click()
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByTestId('payment-status')).toHaveText('Tölendi')

  await page.getByTestId('refund-request').click()
  await page.getByTestId('refund-amount').fill('20')
  await page.getByTestId('refund-reason').fill('Harytda kemçilik bar')
  await page.getByTestId('drawer-submit').click()

  await expect(dialog).toContainText('20,00')
  await dialog.getByRole('button', { name: 'Pul gaýtarmak' }).click()
  await expect(page.getByTestId('refund')).toContainText('Harytda kemçilik bar')
  await expect(page.getByTestId('refund')).toContainText('Tamamlandy')
  await expect(page.getByTestId('payment-status')).toHaveText('Bölekleýin gaýtaryldy')

  // A refund over what was paid is the server's to refuse, and the reason must reach the user.
  await page.getByTestId('refund-request').click()
  await page.getByTestId('refund-amount').fill('9999')
  await page.getByTestId('refund-reason').fill('Gaty köp')
  await page.getByTestId('drawer-submit').click()
  await dialog.getByRole('button', { name: 'Pul gaýtarmak' }).click()
  await expect(dialog.getByRole('alert')).toBeVisible()
  await dialog.getByRole('button', { name: 'Ýatyr', exact: true }).click()
  await expect(page.getByTestId('confirm-message')).toHaveCount(0)
  await page.getByRole('dialog').getByRole('button', { name: 'Ýatyr', exact: true }).click()
  await expect(page.getByRole('dialog')).toHaveCount(0)

  const other = await place()

  await page.goto(`${ADMIN_URL}/tk/orders/${other.id}`)
  await page.getByTestId('order-action-cancelled').click()
  await page.getByTestId('drawer-submit').click()

  const cancel = page.getByRole('dialog').filter({ hasText: 'ýatyrylsynmy' })

  await expect(cancel).toBeVisible()
  await expect(page.getByTestId('order-status')).toHaveText('Garaşylýar')
  await cancel.getByRole('button', { name: 'Sargydy ýatyr' }).click()
  await expect(page.getByTestId('order-status')).toHaveText('Ýatyryldy')
  await expect(page.getByTestId('order-actions')).toHaveCount(0)
  await api.dispose()
})

test('a large refund needs a second person, who can approve it', async ({ page, browser }) => {
  const { api, place } = await storeWithOrder(page, 'Tassyklama Dükany')

  await ok(
    await api.put('/api/v1/admin/store/payment-settings', {
      data: {
        acceptsCashOnDelivery: true,
        acceptsBankTransfer: false,
        bankTransferInstructions: null,
        refundApprovalThreshold: 5,
      },
    }),
  )

  const order = await place()

  await page.goto(`${ADMIN_URL}/tk/orders/${order.id}`)
  await page.getByTestId('payment-confirm').click()
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByTestId('payment-status')).toHaveText('Tölendi')

  const requestRefund = async (amount: string, reason: string) => {
    await page.getByTestId('refund-request').click()
    await page.getByTestId('refund-amount').fill(amount)
    await page.getByTestId('refund-reason').fill(reason)
    await page.getByTestId('drawer-submit').click()
    await page
      .getByRole('dialog')
      .filter({ hasText: 'yzyna alyp bolmaýar' })
      .getByRole('button', { name: 'Pul gaýtarmak' })
      .click()
  }

  const refunds = page.getByTestId('refund')

  await requestRefund('10', 'Birinji sebäp')

  const first = refunds.filter({ hasText: 'Birinji sebäp' })

  await expect(first).toContainText('Tassyklama garaşýar')

  // The server refuses the person who asked, so the button is not even offered to them.
  await expect(first.getByTestId('refund-approve')).toHaveCount(0)
  await expect(first.getByTestId('refund-own')).toBeVisible()

  await first.getByTestId('refund-reject').click()
  await page.getByTestId('reject-note').fill('Delil ýok')
  await page.getByTestId('drawer-submit').click()
  await expect(first).toContainText('Ret edildi')

  await requestRefund('15', 'Ikinji sebäp')
  await expect(refunds.filter({ hasText: 'Ikinji sebäp' })).toContainText('Tassyklama garaşýar')

  const manager = await addStaffMember(api, browser, 'Manager')

  await manager.page.goto(`${ADMIN_URL}/tk/orders/${order.id}`)

  const pending = manager.page.getByTestId('refund').filter({ hasText: 'Ikinji sebäp' })

  // A manager decides on refunds, and cannot ask for one.
  await expect(manager.page.getByTestId('refund-request')).toHaveCount(0)
  await pending.getByTestId('refund-approve').click()
  await manager.page
    .getByRole('dialog')
    .filter({ hasText: 'tassyklamalymy' })
    .getByRole('button', { name: 'Tassykla' })
    .click()
  await expect(pending).toContainText('Tamamlandy')
  await expect(manager.page.getByTestId('payment-refunded')).toContainText('15,00')
  await manager.context.close()

  await page.reload()
  await expect(refunds.filter({ hasText: 'Ikinji sebäp' })).toContainText('Tamamlandy')
  await api.dispose()
})

test('the order list filters through the address', async ({ page }) => {
  const { api, place } = await storeWithOrder(page, 'Süzgüç Sargyt Dükany')
  const first = await place({ phone: '+99361000111' })
  const second = await place({ phone: '+99361000222' })

  await ok(
    await api.put(`/api/v1/admin/orders/${first.id}/status`, { data: { status: 'confirmed' } }),
  )

  await page.goto(`${ADMIN_URL}/tk/orders`)
  await expect(page.getByTestId('order-link')).toHaveCount(2)

  await choose(page, 'filter-status', 'Tassyklandy')
  await expect(page).toHaveURL(/status=confirmed/)
  await expect(page.getByTestId('order-link')).toHaveCount(1)
  await expect(page.getByTestId('order-link')).toHaveText(first.number)

  await page.getByRole('button', { name: 'Süzgüçleri arassala' }).click()
  await expect(page.getByTestId('order-link')).toHaveCount(2)

  await choose(page, 'filter-payment', 'Tölenmedik')
  await expect(page).toHaveURL(/paymentState=unpaid/)
  await expect(page.getByTestId('order-link')).toHaveCount(2)
  await page.getByRole('button', { name: 'Süzgüçleri arassala' }).click()

  await page.getByTestId('filter-search').fill(second.number)
  await page.getByTestId('filter-search').press('Enter')
  await expect(page).toHaveURL(new RegExp(`q=${second.number}`))
  await expect(page.getByTestId('order-link')).toHaveCount(1)
  await expect(page.getByTestId('order-link')).toHaveText(second.number)
  await page.getByRole('button', { name: 'Süzgüçleri arassala' }).click()

  // A range ending yesterday cannot hold orders placed today.
  await page.goto(`${ADMIN_URL}/tk/orders?to=2020-01-01`)
  await expect(page.getByText('Bu süzgüçlere laýyk sargyt ýok')).toBeVisible()

  const today = new Date().toISOString().slice(0, 10)

  await page.goto(`${ADMIN_URL}/tk/orders?from=${today}`)
  await expect(page.getByTestId('order-link')).toHaveCount(2)

  // The picker writes to the address too, and the back button undoes it.
  await page.goto(`${ADMIN_URL}/tk/orders`)
  await expect(page.getByTestId('order-link')).toHaveCount(2)
  await page.getByPlaceholder('Soňky sene').fill('2020-01-01')
  await page.getByPlaceholder('Soňky sene').press('Enter')
  await expect(page).toHaveURL(/to=2020-01-01/)
  await expect(page.getByText('Bu süzgüçlere laýyk sargyt ýok')).toBeVisible()
  await page.goBack()
  await expect(page).not.toHaveURL(/to=/)
  await expect(page.getByTestId('order-link')).toHaveCount(2)
  await api.dispose()
})

test('customers list their orders, and a guest’s profile keeps the phone out of the address', async ({
  page,
}) => {
  const { api, place } = await storeWithOrder(page, 'Müşderi Dükany')

  const order = await place({ phone: '+99361555444', name: 'Maral Annaýewa' })

  await place({ phone: '+99361555444', name: 'Maral Annaýewa' })
  await place({ phone: '+99361777888', name: 'Başga Adam' })

  await page.goto(`${ADMIN_URL}/tk/customers`)
  await expect(page.getByTestId('customer-link')).toHaveCount(2)

  await page.getByTestId('filter-search').fill('+99361555444')
  await page.getByTestId('filter-search').press('Enter')
  await expect(page.getByTestId('customer-link')).toHaveCount(1)
  await page.getByTestId('customer-link').click()

  await expect(page).toHaveURL(/\/customers\/guest\/[0-9a-f-]{36}$/)
  expect(page.url()).not.toContain('99361555444')
  await expect(page.getByRole('heading', { name: 'Maral Annaýewa' })).toBeVisible()
  await expect(page.getByTestId('customer-order-count')).toHaveText('2')
  await expect(page.getByRole('link', { name: order.number })).toBeVisible()
  await api.dispose()
})

test('a customer with an account has a profile with all their orders', async ({ page }) => {
  const { api, owner, store } = await storeWithOrder(page, 'Hasaply Müşderi Dükany')
  const email = await createConfirmedUser('Hasaply Müşderi')
  const sale = { variantId: store.product.variantId, shippingMethodId: store.shippingMethodId }
  const first = await placeCustomerOrder(owner.slug, email, sale)

  await placeCustomerOrder(owner.slug, email, { ...sale })

  await page.goto(`${ADMIN_URL}/tk/orders/${first.id}`)
  await page.getByTestId('order-contact').getByRole('link').click()
  await expect(page).toHaveURL(/\/customers\/[0-9a-f-]{36}$/)
  expect(page.url()).not.toContain(first.phone.slice(1))
  await expect(page.getByRole('heading', { name: 'Hasaply Müşderi' })).toBeVisible()
  await expect(page.getByTestId('customer-summary')).toContainText(email)
  await expect(page.getByRole('link', { name: first.number })).toBeVisible()
  await expect(page.getByRole('row')).toHaveCount(3)
  await expect(page.getByText('Hasaby bar')).toBeVisible()
  await api.dispose()
})

test('without customers.read, customer details are hidden on every page that has them', async ({
  page,
}) => {
  const { api, place } = await storeWithOrder(page, 'Gizlin Müşderi Dükany')
  const order = await place({ phone: '+99361999888', name: 'Ogulgerek Hojaýewa' })

  await hidePermissionFromPage(page, 'customers.read')
  await page.goto(`${ADMIN_URL}/tk/orders/${order.id}`)
  await expect(page.getByRole('heading', { name: `Sargyt ${order.number}` })).toBeVisible()

  const contact = page.getByTestId('order-contact')
  const main = page.locator('main')

  await expect(contact).toContainText('O•••')
  await expect(contact).toContainText('Müşderiniň maglumatlaryny doly görmek üçin ygtyýaryňyz ýok')
  await expect(main).not.toContainText('Hojaýewa')
  await expect(main).not.toContainText('999888')
  await expect(main).not.toContainText('ayna@example.com')
  await expect(main).not.toContainText('Magtymguly')
  // There is no profile to go to.
  await expect(contact.getByRole('link')).toHaveCount(0)

  await page.goto(`${ADMIN_URL}/tk/orders`)
  await expect(page.getByTestId('order-link')).toHaveText(order.number)
  await expect(main).not.toContainText('Hojaýewa')

  await page.goto(`${ADMIN_URL}/tk`)
  await expect(page.getByRole('link', { name: order.number })).toBeVisible()
  await expect(main).not.toContainText('Hojaýewa')
  await expect(page.getByTestId('tile-customers')).toHaveCount(0)

  await page.goto(`${ADMIN_URL}/tk/customers`)
  await expect(page).toHaveURL(/\/tk\/forbidden$/)
  await api.dispose()
})

test('the dashboard shows real counts and a chart from the API', async ({ page }) => {
  const { api, place } = await storeWithOrder(page, 'Görkeziji Dükany')

  await place()
  await place()

  await page.goto(`${ADMIN_URL}/tk`)
  await expect(page.getByTestId('tile-pending').getByTestId('tile-value')).toHaveText('2')
  await expect(page.getByTestId('tile-unpaid').getByTestId('tile-value')).toHaveText('2')
  await expect(page.getByTestId('tile-products').getByTestId('tile-value')).toHaveText('1')
  await expect(page.getByTestId('tile-customers').getByTestId('tile-value')).toHaveText('2')
  await expect(page.getByTestId('orders-chart').locator('canvas')).toBeVisible()
  // The same numbers in text form: 14 days, and today's two orders in the last one.
  await expect(page.locator('table.sr-only tr')).toHaveCount(14)
  await expect(page.locator('table.sr-only tr').last().locator('td')).toHaveText('2')

  await page.getByTestId('tile-pending').click()
  await expect(page).toHaveURL(/\/orders\?status=pending/)
  await api.dispose()
})

test('a new order raises a notification, and the check pauses while the tab is hidden', async ({
  page,
}) => {
  // Runs at normal speed until told otherwise, then jumps over the minute between checks.
  await page.clock.install()

  const { api, place } = await storeWithOrder(page, 'Bildiriş Dükany')
  const badge = page.getByTestId('order-badge').locator('sup')
  const checks: string[] = []

  page.on('request', (sent) => {
    if (sent.url().includes('/admin/orders') && sent.url().includes('pageSize=5')) {
      checks.push(sent.url())
    }
  })

  await page.goto(`${ADMIN_URL}/tk/stock`)
  await expect.poll(() => checks.length).toBeGreaterThan(0)
  await expect(badge).toBeHidden()

  const order = await place()

  const setHidden = (hidden: boolean) =>
    page.evaluate((isHidden) => {
      Object.defineProperty(document, 'visibilityState', {
        configurable: true,
        get: () => (isHidden ? 'hidden' : 'visible'),
      })
      document.dispatchEvent(new Event('visibilitychange'))
    }, hidden)

  await setHidden(true)

  const beforeHidden = checks.length

  await page.clock.fastForward('02:05')
  await page.waitForTimeout(500)
  expect(checks.length).toBe(beforeHidden)
  await expect(badge).toBeHidden()

  await setHidden(false)
  await page.clock.fastForward('01:05')
  await expect(badge).toHaveText('1')

  const notice = page.locator('.order-notification')

  await expect(notice).toContainText(order.number)

  // The list says what was new, and takes the user there.
  await page.getByTestId('order-bell').click()
  await page.getByRole('button', { name: order.number }).click()
  await expect(page).toHaveURL(new RegExp(`/orders/${order.id}$`))
  await expect(badge).toBeHidden()
  await api.dispose()
})
