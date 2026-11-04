import { request } from '@playwright/test'
import { signInAsNewOwner } from './support/admin'
import { adminApi, choose, ok, seedSellableStore, watchToken } from './support/admin-data'
import { expect, test } from './support/fixtures'
import { ADMIN_URL, API_URL } from './support/stack'

test('general settings save, and a colleague’s change is not overwritten', async ({ page }) => {
  test.slow()

  const token = watchToken(page)

  await signInAsNewOwner(page, 'Sazlama Dükany')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()
  await page.goto(`${ADMIN_URL}/tk/settings`)

  await page.getByTestId('settings-name').fill('Täze at')
  await page.getByTestId('settings-save').click()
  await expect(page.getByText('Sazlamalar saklandy')).toBeVisible()

  const api = await adminApi(token.current())
  const saved = await ok<{
    name: string
    defaultCurrency: string
    defaultLanguage: string
    version: number
  }>(await api.get('/api/v1/admin/store'))

  expect(saved.name).toBe('Täze at')

  await ok(
    await api.put('/api/v1/admin/store', {
      data: {
        name: 'Kärdeşiň ady',
        defaultCurrency: saved.defaultCurrency,
        defaultLanguage: saved.defaultLanguage,
        logoUrl: null,
        contactEmail: null,
        contactPhone: null,
        address: null,
        workingHours: null,
        version: saved.version,
      },
    }),
  )

  await page.getByTestId('settings-name').fill('Meniň adym')
  await page.getByTestId('settings-save').click()
  await expect(page.getByRole('alert').first()).toBeVisible()
  await expect((await ok<{ name: string }>(await api.get('/api/v1/admin/store'))).name).toBe(
    'Kärdeşiň ady',
  )
  await api.dispose()
})

test('the default language is chosen from the store’s languages', async ({ page }) => {
  const token = watchToken(page)

  await signInAsNewOwner(page, 'Dil Dükany')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()
  await page.goto(`${ADMIN_URL}/tk/settings?tab=languages`)

  await choose(page, 'settings-language', 'Rusça')
  await page.getByTestId('settings-language-save').click()
  await expect(page.getByText('Sazlamalar saklandy')).toBeVisible()

  const api = await adminApi(token.current())
  const saved = await ok<{ defaultLanguage: string }>(await api.get('/api/v1/admin/store'))

  expect(saved.defaultLanguage).toBe('ru')
  await api.dispose()
})

test('payment settings take effect at the checkout', async ({ page }) => {
  test.slow()

  const token = watchToken(page)
  const owner = await signInAsNewOwner(page, 'Töleg Dükany')

  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  const api = await adminApi(token.current())
  const store = await seedSellableStore(api)

  await page.goto(`${ADMIN_URL}/tk/settings?tab=payment`)

  // A new store takes cash only, and the form shows what is really set.
  await expect(page.getByRole('switch', { name: 'Alanda nagt' })).toBeChecked()
  await expect(page.getByTestId('settings-bank-text')).toHaveCount(0)

  await page.getByTestId('settings-bank').click()
  await page.getByTestId('settings-payment-save').click()
  await expect(page.getByText('görkezme gerek')).toBeVisible()
  await page.getByTestId('settings-bank-text').fill('Hasap: 1234')
  await page.getByTestId('settings-payment-save').click()
  await expect(page.getByText('Sazlamalar saklandy')).toBeVisible()

  // Coming back shows what was saved.
  await page.reload()
  await expect(page.getByTestId('settings-bank-text')).toHaveValue('Hasap: 1234')

  const guest = await request.newContext({
    baseURL: API_URL,
    extraHTTPHeaders: { 'X-Tenant': owner.slug, 'X-Requested-With': 'ComTest' },
  })

  await ok(
    await guest.post('/api/v1/cart/items', {
      data: { variantId: store.product.variantId, quantity: 1 },
    }),
  )

  const quote = await ok<{ paymentOptions: { code: string; instructions: string | null }[] }>(
    await guest.post('/api/v1/checkout/quote', { data: { region: 'ashgabat' } }),
  )
  const bank = quote.paymentOptions.find((option) => option.code === 'bankTransfer')

  expect(bank?.instructions).toBe('Hasap: 1234')
  await guest.dispose()
  await api.dispose()
})

test('shipping zones and methods are managed, and deleting asks first', async ({ page }) => {
  test.slow()

  await signInAsNewOwner(page, 'Eltip Dükany')
  await page.goto(`${ADMIN_URL}/tk/settings?tab=shipping`)

  await page.getByTestId('new-zone').click()
  await page.getByTestId('zone-name').fill('Aşgabat zolagy')
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByText('Azyndan bir sebit saýlaň')).toBeVisible()
  await choose(page, 'zone-regions', 'Aşgabat')
  await page.getByText('Täze zolak').last().click()
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByTestId('zone-row')).toHaveText('Aşgabat zolagy')

  await page.getByTestId('new-method').click()
  await page.getByTestId('method-name-tk').fill('Şäher içi kurýer')
  await page.getByTestId('method-price').fill('15.50')
  await choose(page, 'method-zone', 'Aşgabat zolagy')
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByTestId('method-row')).toHaveText('Şäher içi kurýer')
  await expect(page.getByRole('row').filter({ hasText: 'Şäher içi kurýer' })).toContainText('15,50')

  await page.getByTestId('new-method').click()
  await page.getByTestId('method-type').getByText('Özüň almak').click()
  await page.getByTestId('method-name-tk').fill('Dükandan alyp gitmek')
  await page.getByTestId('method-pickup').fill('Magtymguly şaýoly 1')
  await page.getByTestId('method-price').fill('0')
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByTestId('method-row')).toHaveCount(2)

  await page
    .getByRole('row')
    .filter({ hasText: 'Dükandan alyp gitmek' })
    .getByTestId('method-delete')
    .click()
  await page.getByRole('dialog').getByRole('button', { name: 'Poz' }).click()
  await expect(page.getByTestId('method-row')).toHaveCount(1)
})
