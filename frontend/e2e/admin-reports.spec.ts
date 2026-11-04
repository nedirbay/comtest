import { readFile } from 'node:fs/promises'
import {
  addStaffMember,
  adminApi,
  placeGuestOrder,
  seedSellableStore,
  watchToken,
} from './support/admin-data'
import { signInAsNewOwner } from './support/admin'
import { expect, test } from './support/fixtures'
import { ADMIN_URL } from './support/stack'

test.describe.configure({ timeout: 300_000 })

test('the owner sees what was sold and takes it away as a file', async ({ page }) => {
  const token = watchToken(page)
  const owner = await signInAsNewOwner(page, 'Hasabat Dükany')

  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  const api = await adminApi(token.current())
  const store = await seedSellableStore(api)

  await placeGuestOrder(owner.slug, {
    variantId: store.product.variantId,
    shippingMethodId: store.shippingMethodId,
    name: '=SUM(A1:A9)',
  })

  await page.goto(`${ADMIN_URL}/tk/reports`)

  await expect(page.getByRole('heading', { name: 'Hasabatlar' })).toBeVisible()
  await expect(page.getByTestId('figure-orders').getByTestId('figure-value')).toHaveText('1')
  await expect(page.getByTestId('figure-revenue')).toContainText('TMT')
  await expect(page.getByTestId('report-buyers')).toContainText('1')
  await expect(page.getByTestId('report-stock-value')).toBeVisible()
  await expect(page.locator('main')).toContainText('Takmynan gazanç')

  const download = page.waitForEvent('download')

  await page.getByTestId('export-customers-csv').click()

  const file = await download
  const text = await readFile(await file.path(), 'utf8')

  expect(file.suggestedFilename()).toBe('customers.csv')
  // A customer's name cannot become a formula in the owner's spreadsheet.
  expect(text).toContain("'=SUM(A1:A9)")
  expect(text).toContain('Müşderi')

  const spreadsheet = page.waitForEvent('download')

  await page.getByTestId('export-products-xlsx').click()
  expect((await spreadsheet).suggestedFilename()).toBe('products.xlsx')

  // The period lives in the address, so that a report can be sent to a colleague.
  await page.goto(`${ADMIN_URL}/tk/reports?from=2020-01-01&to=2020-01-31`)
  await expect(page.getByTestId('figure-orders').getByTestId('figure-value')).toHaveText('0')
  await expect(page.locator('main')).toContainText('Bu döwürde satuw ýok')

  await api.dispose()
})

test('reports stay out of reach of staff without the permission', async ({ page, browser }) => {
  const token = watchToken(page)
  await signInAsNewOwner(page, 'Işgär Hasabaty')

  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  const api = await adminApi(token.current())
  const staff = await addStaffMember(api, browser, 'Staff')

  await expect(staff.page.getByTestId('dashboard-tiles')).toBeVisible()
  await expect(staff.page.getByTestId('tile-revenue')).toHaveCount(0)
  await expect(staff.page.getByTestId('nav-reports')).toHaveCount(0)

  await staff.page.goto(`${ADMIN_URL}/tk/reports`)
  await expect(staff.page).toHaveURL(/\/forbidden/)

  await staff.context.close()
  await api.dispose()
})
