import { signInAsNewOwner } from './support/admin'
import {
  adminApi,
  choose,
  createProduct,
  ok,
  seedSellableStore,
  unique,
  watchToken,
} from './support/admin-data'
import { expect, test } from './support/fixtures'
import { ADMIN_URL } from './support/stack'

test('warehouses are added, changed and removed, and removing asks first', async ({ page }) => {
  await signInAsNewOwner(page, 'Ammar Dükany')
  await page.goto(`${ADMIN_URL}/tk/warehouses`)

  await page.getByTestId('new-warehouse').click()
  await page.getByTestId('warehouse-name').fill('Merkezi ammar')
  await page.getByTestId('warehouse-code').fill('bad code')
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByText('Diňe latyn harplary')).toBeVisible()
  await page.getByTestId('warehouse-code').fill('MAIN-1')
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByTestId('warehouse-row')).toHaveText('Merkezi ammar')

  await page.getByTestId('warehouse-edit').click()
  await page.getByTestId('warehouse-name').fill('Täze ady')
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByTestId('warehouse-row')).toHaveText('Täze ady')

  await page.getByTestId('warehouse-delete').click()
  await page.getByRole('dialog').getByRole('button', { name: 'Poz' }).click()
  await expect(page.getByTestId('warehouse-row')).toHaveCount(0)
})

test('stock is adjusted with a reason, and low stock can be found', async ({ page }) => {
  test.slow()

  const token = watchToken(page)

  await signInAsNewOwner(page, 'Galyndy Dükany')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  const api = await adminApi(token.current())
  const store = await seedSellableStore(api, { stock: 10 })
  const fresh = await createProduct(api, { name: 'Täze gelen haryt' })

  await page.goto(`${ADMIN_URL}/tk/stock`)

  const rows = page.getByTestId('stock-row')

  await expect(rows).toHaveCount(1)

  // A new product has no stock row yet, so it is found through the catalog.
  await page.getByTestId('adjust-stock').click()
  await choose(page, 'adjust-product', 'Täze gelen haryt')
  await page.getByTestId('adjust-delta').fill('-5')
  await page.getByTestId('adjust-reason').fill('Harytlar geldi')
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByText('Girdeji noldan uly bolmaly')).toBeVisible()
  await page.getByTestId('adjust-delta').fill('3')
  await page.getByTestId('drawer-submit').click()
  await expect(rows).toHaveCount(2)

  const row = page.getByRole('row').filter({ hasText: 'Täze gelen haryt' })

  await expect(row).toContainText(fresh.sku)

  // Nothing counts as low until a threshold is set.
  await page.getByTestId('filter-low').click()
  await expect(page).toHaveURL(/lowOnly=true/)
  await page.getByTestId('filter-low').click()
  await expect(rows).toHaveCount(2)

  await row.getByTestId('stock-threshold').click()
  await page.getByTestId('threshold-input').locator('input').fill('5')
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByText('Çäk saklandy')).toBeVisible()
  await expect(row).toContainText('Az')

  await page.getByTestId('filter-low').click()
  await expect(rows).toHaveCount(1)
  await expect(rows).toContainText('Täze gelen haryt')

  // An adjustment can lower stock, and the server refuses going below what is reserved.
  await row.getByTestId('stock-adjust').click()
  await page.getByTestId('adjust-type').getByText('Düzediş').click()
  await page.getByTestId('adjust-delta').fill('-100')
  await page.getByTestId('adjust-reason').fill('Sanamak')
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByRole('alert').first()).toBeVisible()
  await page.getByTestId('adjust-delta').fill('-1')
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByText('Galyndy üýtgedildi')).toBeVisible()

  // The history tells the same story, from the row's own link.
  await row.getByRole('link', { name: 'Taryh' }).click()
  await expect(page).toHaveURL(/\/stock\/movements\?variantId=/)
  await expect(page.getByTestId('movements-count')).toContainText('2')
  await expect(page.getByTestId('movements-table')).toContainText('Sanamak')
  await expect(page.getByTestId('movements-table')).toContainText('Harytlar geldi')

  expect(store.warehouseId).toBeTruthy()
  await api.dispose()
})

test('a long movement history loads page by page and draws only what is on screen', async ({
  page,
}) => {
  test.setTimeout(180_000)

  const token = watchToken(page)

  await signInAsNewOwner(page, 'Hereket Dükany')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  const api = await adminApi(token.current())
  const store = await seedSellableStore(api)
  const total = 130

  for (let start = 0; start < total; start += 10) {
    await Promise.all(
      Array.from({ length: 10 }, (_, index) =>
        api
          .post('/api/v1/admin/inventory/stock/adjustments', {
            data: {
              warehouseId: store.warehouseId,
              variantId: store.product.variantId,
              delta: 1,
              type: 'receipt',
              reason: unique(`hereket-${start + index}`),
            },
          })
          .then(ok),
      ),
    )
  }

  await page.goto(`${ADMIN_URL}/tk/stock/movements`)

  const count = page.getByTestId('movements-count')
  const table = page.getByTestId('movements-table')

  await expect(count).toContainText('100')
  await expect(count).toContainText(String(total + 1))

  const drawn = await table.locator('.el-table-v2__row').count()

  expect(drawn).toBeGreaterThan(0)
  expect(drawn).toBeLessThan(100)

  // Scrolling to the end asks for the next page, as the API gives at most a hundred at a time.
  await table.hover()
  await page.mouse.wheel(0, 100_000)
  await expect(count).toContainText(`${total + 1} hereketden ${total + 1}`)
  await api.dispose()
})
