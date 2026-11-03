import { mkdirSync } from 'node:fs'
import { join } from 'node:path'
import { signInAsNewOwner } from './support/admin'
import {
  adminApi,
  chooseTab,
  createProduct,
  expectNoSidewaysScroll,
  ok,
  openFromMenu,
  placeGuestOrder,
  realPng,
  seedSellableStore,
  visibleEditor,
  watchToken,
} from './support/admin-data'
import { expect, test } from './support/fixtures'
import { ADMIN_URL } from './support/stack'

/** Set this to keep the screenshots, to look at them. Without it the test only checks the layout. */
const SHOTS = process.env.ADMIN_SHOTS

test('every page fits the screen, in both themes', async ({ page }, testInfo) => {
  test.setTimeout(420_000)

  const token = watchToken(page)
  const owner = await signInAsNewOwner(page, 'Görnüş Dükany')

  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  const api = await adminApi(token.current())
  const store = await seedSellableStore(api)
  const order = await placeGuestOrder(owner.slug, {
    variantId: store.product.variantId,
    shippingMethodId: store.shippingMethodId,
  })

  await placeGuestOrder(owner.slug, {
    variantId: store.product.variantId,
    shippingMethodId: store.shippingMethodId,
  })
  await createProduct(api, { name: 'Bir gaty uzyn atly haryt, sanawda we formada görkezilýär' })
  await createProduct(api, { name: 'Garalama haryt', publish: false })

  const brand = await ok<{ id: string }>(
    await api.post('/api/v1/admin/catalog/brands', {
      data: { name: 'Altyn Asyr', slug: 'altyn-asyr' },
    }),
  )
  const parent = await ok<{ id: string }>(
    await api.post('/api/v1/admin/catalog/categories', {
      data: {
        parentId: null,
        slug: 'egin-esik',
        sortOrder: 0,
        isActive: true,
        translations: [{ language: 'tk', name: 'Egin-eşik' }],
      },
    }),
  )

  await ok(
    await api.post('/api/v1/admin/catalog/categories', {
      data: {
        parentId: parent.id,
        slug: 'koynekler',
        sortOrder: 0,
        isActive: true,
        translations: [{ language: 'tk', name: 'Köýnekler' }],
      },
    }),
  )
  await ok(
    await api.post('/api/v1/admin/catalog/attributes', {
      data: {
        code: 'material',
        dataType: 'select',
        options: ['Pagta', 'Ýüpek'],
        translations: [{ language: 'tk', name: 'Material' }],
      },
    }),
  )
  expect(brand.id).toBeTruthy()

  const pages: [string, string, () => Promise<unknown>][] = [
    ['dashboard', '', () => expect(page.getByTestId('orders-chart')).toBeVisible()],
    ['orders', 'orders', () => expect(page.getByTestId('order-link').first()).toBeVisible()],
    [
      'order-detail',
      `orders/${order.id}`,
      () => expect(page.getByTestId('payment-status')).toBeVisible(),
    ],
    [
      'customers',
      'customers',
      () => expect(page.getByTestId('customer-link').first()).toBeVisible(),
    ],
    ['products', 'products', () => expect(page.getByTestId('product-link').first()).toBeVisible()],
    ['product-new', 'products/new', () => expect(page.getByTestId('product-slug')).toBeVisible()],
    [
      'product-edit',
      `products/${store.product.id}`,
      () => expect(page.getByTestId('product-slug')).toBeVisible(),
    ],
    [
      'categories',
      'categories',
      () => expect(page.getByTestId('category-node').first()).toBeVisible(),
    ],
    ['brands', 'brands', () => expect(page.getByTestId('brand-row').first()).toBeVisible()],
    [
      'attributes',
      'attributes',
      () => expect(page.getByTestId('attribute-row').first()).toBeVisible(),
    ],
    ['stock', 'stock', () => expect(page.getByTestId('stock-row').first()).toBeVisible()],
    [
      'movements',
      'stock/movements',
      () => expect(page.getByTestId('movements-table')).toBeVisible(),
    ],
    [
      'warehouses',
      'warehouses',
      () => expect(page.getByTestId('warehouse-row').first()).toBeVisible(),
    ],
    ['settings', 'settings', () => expect(page.getByTestId('settings-name')).toBeVisible()],
    [
      'settings-payment',
      'settings?tab=payment',
      () => expect(page.getByTestId('payment-no-read')).toBeVisible(),
    ],
    [
      'settings-shipping',
      'settings?tab=shipping',
      () => expect(page.getByTestId('zone-row').first()).toBeVisible(),
    ],
    [
      'settings-languages',
      'settings?tab=languages',
      () => expect(page.getByTestId('settings-language')).toBeVisible(),
    ],
    ['staff', 'staff', () => expect(page.getByTestId('staff-row').first()).toBeVisible()],
    [
      'security',
      'security',
      () => expect(page.getByRole('heading', { name: 'Howpsuzlyk' })).toBeVisible(),
    ],
  ]

  const folder = SHOTS ? join(SHOTS, testInfo.project.name) : null

  if (folder) {
    mkdirSync(folder, { recursive: true })
  }

  for (const theme of ['light', 'dark'] as const) {
    await page.evaluate((next) => {
      window.localStorage.setItem('comtest.pref.theme', next)
    }, theme)

    for (const [name, path, ready] of pages) {
      await page.goto(`${ADMIN_URL}/tk/${path}`)
      await ready()
      // The tables measure themselves after the first frame.
      await page.waitForTimeout(250)
      await expectNoSidewaysScroll(page)

      if (folder) {
        await page.screenshot({ path: join(folder, `${name}-${theme}.png`), fullPage: true })
      }
    }

    // What only appears on the way through a page: the tabs of the form, and everything on top.
    const shot = async (name: string) => {
      await page.waitForTimeout(450)
      await expectNoSidewaysScroll(page)

      if (folder) {
        await page.screenshot({ path: join(folder, `${name}-${theme}.png`) })
      }
    }

    await page.goto(`${ADMIN_URL}/tk/products/${store.product.id}`)
    await expect(page.getByTestId('product-slug')).toBeVisible()
    await chooseTab(page, 'Suratlar')
    await page.getByTestId('image-input').setInputFiles({
      name: 'surat.png',
      mimeType: 'image/png',
      buffer: await realPng(page),
    })
    await expect(page.getByTestId('image-thumb').first()).toBeVisible({ timeout: 60_000 })
    await shot('form-images')
    await chooseTab(page, 'Tekstler')
    await expect(visibleEditor(page)).toBeVisible()
    await visibleEditor(page).click()
    await page.keyboard.type('Düşündiriş ýazylýar.')
    await page.getByTestId('short-tk').click()
    await shot('form-texts')
    await chooseTab(page, 'Wariantlar')
    await page.getByTestId('option-add').click()
    await shot('form-variants')
    await chooseTab(page, 'Häsiýetler')
    await shot('form-attributes')
    await chooseTab(page, 'SEO')
    await shot('form-seo')

    // Leaving with those edits up raises the question.
    await openFromMenu(page, 'products')
    await expect(page.getByText('Saklanmadyk üýtgeşmeler')).toBeVisible()
    await shot('unsaved-dialog')
    await page.getByRole('button', { name: 'Saklamazdan çyk' }).click()

    await expect(page.getByTestId('product-link').first()).toBeVisible()
    await page.getByRole('row').nth(1).locator('.el-checkbox').click()
    await shot('bulk-bar')
    await page.getByTestId('bulk-delete').click()
    await shot('confirm-dialog')
    await page.getByRole('dialog').getByRole('button', { name: 'Ýatyr' }).click()
    await expect(page.getByTestId('confirm-message')).toHaveCount(0)
    await page.getByTestId('column-chooser').click()
    await shot('column-chooser')
    await page.keyboard.press('Escape')

    await page.goto(`${ADMIN_URL}/tk/warehouses`)
    await page.getByTestId('new-warehouse').click()
    await page.getByTestId('drawer-submit').click()
    await shot('drawer-errors')

    await page.goto(`${ADMIN_URL}/tk`)
    await expect(page.getByTestId('dashboard-tiles')).toBeVisible()
    await page.getByTestId('palette-button').click()
    await page.getByTestId('palette-input').fill('ha')
    await shot('palette')
    await page.keyboard.press('Escape')
    await page.getByTestId('order-bell').click()
    await shot('order-bell')

    if (await page.getByTestId('menu-button').isVisible()) {
      await page.goto(`${ADMIN_URL}/tk/orders`)
      await page.getByTestId('menu-button').click()
      await shot('phone-menu')
    }
  }

  await api.dispose()
})
