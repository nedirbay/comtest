import type { Page } from '@playwright/test'
import { signInAsNewOwner } from './support/admin'
import { adminApi, choose, chooseTab, ok, unique, watchToken } from './support/admin-data'
import { expect, test } from './support/fixtures'
import { ADMIN_URL } from './support/stack'

interface CategoryDto {
  id: string
  parentId: string | null
  slug: string
  sortOrder: number
  translations: { language: string; name: string }[]
}

async function createCategory(page: Page, name: string): Promise<void> {
  await page.getByTestId('new-category').click()
  await page.getByTestId('category-name-tk').fill(name)
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByTestId('category-node').filter({ hasText: name })).toBeVisible()
  // The panel covers the tree until it has slid away.
  await expect(page.getByRole('dialog')).toHaveCount(0)
}

test('categories form a tree that can be rearranged by dragging', async ({ page }, testInfo) => {
  test.slow()
  test.skip(
    testInfo.project.name === 'phone',
    'Dragging is for a mouse. Phones use the edit panel.',
  )

  const token = watchToken(page)

  await signInAsNewOwner(page, 'Kategoriýa Dükany')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()
  await page.goto(`${ADMIN_URL}/tk/categories`)

  await createCategory(page, 'Egin-eşik')
  await createCategory(page, 'Köýnekler')
  await createCategory(page, 'Aýakgap')

  const node = (name: string) => page.locator('.el-tree-node__content').filter({ hasText: name })

  // The tree reads the pointer on each dragover, so the last stretch has to be real movement.
  const drag = async (from: string, to: string, height: number) => {
    const source = (await node(from).boundingBox())!
    const target = (await node(to).boundingBox())!

    await page.mouse.move(source.x + source.width / 2, source.y + source.height / 2)
    await page.mouse.down()
    await page.mouse.move(target.x + target.width / 2, target.y + target.height / 2, { steps: 5 })
    await page.mouse.move(target.x + target.width / 2, target.y + target.height * height, {
      steps: 5,
    })
    await page.mouse.up()
  }

  // Into another category: the middle of a row means inside it.
  await drag('Köýnekler', 'Egin-eşik', 0.5)
  await expect(page.getByText('Tertip saklandy')).toBeVisible()

  const api = await adminApi(token.current())

  const list = async () => ok<CategoryDto[]>(await api.get('/api/v1/admin/catalog/categories'))
  const named = (categories: CategoryDto[], name: string) =>
    categories.find((item) => item.translations[0]!.name === name)!

  await expect
    .poll(async () => {
      const categories = await list()

      return named(categories, 'Köýnekler').parentId === named(categories, 'Egin-eşik').id
    })
    .toBe(true)

  // Above a neighbour: the top edge of a row means before it. Sorted by address until now, the
  // shoes came first.
  await page.reload()
  await expect(node('Aýakgap')).toBeVisible()
  await drag('Egin-eşik', 'Aýakgap', 0.08)

  await expect
    .poll(async () => {
      const categories = await list()

      return named(categories, 'Egin-eşik').sortOrder < named(categories, 'Aýakgap').sortOrder
    })
    .toBe(true)

  await page.reload()
  await expect(page.getByTestId('category-node').first()).toContainText('Egin-eşik')
  await api.dispose()
})

test('a category keeps its translations, and a parent with children cannot go', async ({
  page,
}) => {
  await signInAsNewOwner(page, 'Terjime Dükany')
  await page.goto(`${ADMIN_URL}/tk/categories`)
  await createCategory(page, 'Ýokarky')

  await page.getByTestId('category-edit').click()
  await page.getByRole('dialog').getByText('Rusça').click()
  await page.getByTestId('category-name-ru').fill('Верхняя')
  await page.getByTestId('drawer-submit').click()
  // The message from creating it can still be showing, and only the closed panel proves this save.
  await expect(page.getByRole('dialog')).toBeHidden()

  await page.goto(`${ADMIN_URL}/ru/categories`)
  await expect(page.getByTestId('category-node')).toContainText('Верхняя')

  await page.goto(`${ADMIN_URL}/tk/categories`)
  await page.getByRole('button', { name: 'Içine kategoriýa goş' }).click()
  await page.getByTestId('category-name-tk').fill('Içki')
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByTestId('category-node').filter({ hasText: 'Içki' })).toBeVisible()

  await page
    .locator('.el-tree-node__content')
    .filter({ hasText: 'Ýokarky' })
    .getByTestId('category-delete')
    .click()

  const dialog = page.getByRole('dialog')

  await dialog.getByRole('button', { name: 'Poz' }).click()
  // The server's refusal reaches the user, and the dialog stays until they answer.
  await expect(dialog.getByRole('alert')).toBeVisible()
  await dialog.getByRole('button', { name: 'Ýatyr' }).click()
  await expect(page.getByTestId('category-node')).toHaveCount(2)
})

test('brands can be added, changed and removed', async ({ page }) => {
  await signInAsNewOwner(page, 'Marka Dükany')
  await page.goto(`${ADMIN_URL}/tk/brands`)

  await page.getByTestId('new-brand').click()
  await page.getByTestId('brand-name').fill('Altyn Asyr')
  await expect(page.getByTestId('brand-slug')).toHaveValue('altyn-asyr')
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByTestId('brand-row')).toHaveText('Altyn Asyr')

  await page.getByTestId('brand-edit').click()
  await page.getByTestId('brand-name').fill('Gök Asman')
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByTestId('brand-row')).toHaveText('Gök Asman')

  await page.getByTestId('brand-delete').click()
  await expect(page.getByRole('dialog')).toContainText('Gök Asman')
  await page.getByRole('dialog').getByRole('button', { name: 'Poz' }).click()
  await expect(page.getByTestId('brand-row')).toHaveCount(0)
})

test('a required category attribute is asked for by the product form', async ({ page }) => {
  test.slow()

  const token = watchToken(page)

  await signInAsNewOwner(page, 'Häsiýet Dükany')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  await page.goto(`${ADMIN_URL}/tk/attributes`)
  await page.getByTestId('new-attribute').click()
  await page.getByTestId('attribute-name-tk').fill('Material')
  await page.getByTestId('attribute-code').fill('material')
  await choose(page, 'attribute-type', 'Saýlaw')
  await page.getByTestId('attribute-options').locator('input').first().fill('Pagta')
  await page.keyboard.press('Enter')
  await page.getByTestId('attribute-options').locator('input').first().fill('Ýüpek')
  await page.keyboard.press('Enter')
  await page.getByRole('dialog').getByText('Täze häsiýet').click()
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByTestId('attribute-row')).toHaveText('Material')

  await page.goto(`${ADMIN_URL}/tk/categories`)
  await createCategory(page, 'Mata')
  await page.getByTestId('category-edit').click()
  await choose(page, 'category-attribute', 'Material')
  await page.getByText('Hökmany').click()
  await page.getByTestId('category-attribute-link').click()
  await expect(page.getByText('Häsiýet baglandy')).toBeVisible()
  await page.getByRole('button', { name: 'Ýatyr' }).click()

  await page.goto(`${ADMIN_URL}/tk/products/new`)
  await choose(page, 'product-category', 'Mata')
  await chooseTab(page, 'Tekstler')
  await page.getByTestId('name-tk').fill('Mata haryt')
  await chooseTab(page, 'Esasy')
  await page.getByTestId('product-slug').fill(unique('mata-haryt'))
  await chooseTab(page, 'Wariantlar')
  await page.getByTestId('variant-sku').fill(unique('MAT'))
  await page.getByTestId('variant-price').fill('50')
  await page.getByTestId('product-save').click()

  // The server knows what the category requires, and says so.
  await expect(page.getByRole('alert').first()).toBeVisible()
  await expect(page).toHaveURL(/products\/new$/)

  await chooseTab(page, 'Häsiýetler')
  await choose(page, 'attribute-material', 'Ýüpek')
  await page.getByTestId('product-save').click()
  await expect(page).toHaveURL(/\/tk\/products\/[0-9a-f-]{36}$/)

  const api = await adminApi(token.current())
  const saved = await ok<{ attributes: { value: string }[] }>(
    await api.get(`/api/v1/admin/catalog/products/${page.url().split('/').pop()}`),
  )

  expect(saved.attributes[0]!.value).toBe('Ýüpek')
  await api.dispose()
})
