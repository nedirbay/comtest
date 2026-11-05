import { signInAsNewOwner } from './support/admin'
import {
  adminApi,
  choose,
  chooseTab,
  createProduct,
  ok,
  openFromMenu,
  realPng,
  unique,
  visibleEditor,
  watchToken,
} from './support/admin-data'
import { expect, test } from './support/fixtures'
import { ADMIN_URL } from './support/stack'

interface ProductDto {
  id: string
  slug: string
  status: string
  version: number
  translations: {
    language: string
    name: string
    description: string | null
    metaTitle: string | null
  }[]
  variants: {
    id: string
    sku: string
    barcode: string | null
    price: number
    compareAtAmount: number | null
    costAmount: number | null
    options: Record<string, string>
  }[]
  images: { mediaAssetId: string; altText: string | null; medium: string | null }[]
}

test('a product made in the form keeps its translations, variant and uploaded image', async ({
  page,
}) => {
  test.slow()

  const token = watchToken(page)

  await signInAsNewOwner(page, 'Haryt Dükany')
  await page.goto(`${ADMIN_URL}/tk/products/new`)

  await chooseTab(page, 'Tekstler')
  await page.getByTestId('name-tk').fill('Pagta köýnek')
  await page.getByTestId('short-tk').fill('Ýumşak we ýeňil')

  const editorTk = visibleEditor(page)

  await expect(editorTk).toBeVisible()
  await editorTk.click()
  await page.keyboard.type('Bu köýnek ')
  await page.getByTestId('editor-bold').locator('visible=true').click()
  await page.keyboard.type('pagtadan')
  await page.getByTestId('editor-bold').locator('visible=true').click()
  await page.keyboard.type(' tikilen.')

  await page.getByTestId('language-tabs').getByText('Rusça').click()
  await page.getByTestId('name-ru').fill('Хлопковая рубашка')
  await visibleEditor(page).click()
  await page.keyboard.type('Рубашка из хлопка.')

  await chooseTab(page, 'SEO')
  await page.getByTestId('meta-title-tk').fill('Pagta köýnek satyn al')

  await chooseTab(page, 'Esasy')
  await expect(page.getByTestId('product-slug')).toHaveValue('pagta-koynek')

  const slug = unique('pagta-koynek')

  await page.getByTestId('product-slug').fill(slug)

  await chooseTab(page, 'Wariantlar')

  const sku = unique('KOY').toUpperCase()

  await page.getByTestId('variant-sku').fill(sku)
  await page.getByTestId('variant-barcode').fill('4870000000011')
  await page.getByTestId('variant-price').fill('149.90')
  await page.getByTestId('variant-old-price').fill('199.00')
  await page.getByTestId('variant-cost').fill('80.50')
  await page.getByTestId('option-add').click()
  await page.getByTestId('option-key').fill('Reňk')
  await page.getByTestId('option-value').fill('Gök')

  await chooseTab(page, 'Suratlar')
  await page.getByTestId('image-input').setInputFiles({
    name: 'koynek.png',
    mimeType: 'image/png',
    buffer: await realPng(page),
  })

  // The real pipeline: signed upload to storage, confirmation, and processing by the server.
  await expect(page.getByTestId('image-thumb')).toBeVisible({ timeout: 60_000 })
  await expect(page.getByTestId('upload-row')).toHaveCount(0)

  const naturalWidth = await page
    .getByTestId('image-thumb')
    .evaluate((image: HTMLImageElement) => image.decode().then(() => image.naturalWidth))

  expect(naturalWidth).toBeGreaterThan(0)

  await page.getByTestId('product-save').click()
  await expect(page).toHaveURL(/\/tk\/products\/[0-9a-f-]{36}$/)

  const id = page.url().split('/').pop()!
  const api = await adminApi(token.current())
  const saved = await ok<ProductDto>(await api.get(`/api/v1/admin/catalog/products/${id}`))

  expect(saved.slug).toBe(slug)

  const tk = saved.translations.find((item) => item.language === 'tk')!
  const ru = saved.translations.find((item) => item.language === 'ru')!

  expect(tk.name).toBe('Pagta köýnek')
  expect(tk.description).toContain('<strong>pagtadan</strong>')
  expect(tk.metaTitle).toBe('Pagta köýnek satyn al')
  expect(ru.name).toBe('Хлопковая рубашка')
  expect(ru.description).toContain('Рубашка из хлопка.')
  // Nothing was written in English, so no empty translation may be made up for it.
  expect(saved.translations.find((item) => item.language === 'en')).toBeUndefined()

  expect(saved.variants).toHaveLength(1)
  expect(saved.variants[0]).toMatchObject({
    sku,
    barcode: '4870000000011',
    price: 149.9,
    compareAtAmount: 199,
    costAmount: 80.5,
    options: { Reňk: 'Gök' },
  })
  expect(saved.images).toHaveLength(1)
  expect(saved.images[0]!.medium).toBeTruthy()

  // A save leaves nothing unsaved behind, so moving away does not ask.
  await openFromMenu(page, 'products')
  await expect(page.getByRole('dialog')).toHaveCount(0)
  await expect(page).toHaveURL(/\/tk\/products$/)
  await expect(page.getByTestId('product-link').first()).toBeVisible()

  await api.dispose()
})

test('images upload by dropping, can be reordered, and bad files are refused', async ({
  page,
}, testInfo) => {
  test.slow()

  const token = watchToken(page)

  await signInAsNewOwner(page, 'Surat Dükany')
  await page.goto(`${ADMIN_URL}/tk/products/new`)
  await chooseTab(page, 'Suratlar')

  const png = await realPng(page)

  // The browser's own check, before anything is sent anywhere.
  const requests: string[] = []

  page.on('request', (sent) => requests.push(sent.url()))
  await page.getByTestId('image-input').setInputFiles({
    name: 'notes.txt',
    mimeType: 'text/plain',
    buffer: Buffer.from('not an image'),
  })
  await expect(page.getByTestId('upload-row')).toContainText('Diňe JPEG, PNG we WebP')
  await page.getByTestId('image-input').setInputFiles({
    name: 'huge.png',
    mimeType: 'image/png',
    buffer: Buffer.alloc(10 * 1024 * 1024 + 1),
  })
  await expect(page.getByTestId('upload-row').last()).toContainText('10 MB-dan uly')
  expect(requests.filter((url) => url.includes('/media/uploads'))).toHaveLength(0)

  // A real drop, with a real file in the drag's data.
  const transfer = await page.evaluateHandle((base64) => {
    const data = new DataTransfer()
    const bytes = Uint8Array.from(atob(base64), (char) => char.charCodeAt(0))

    data.items.add(new File([bytes], 'dropped.png', { type: 'image/png' }))

    return data
  }, png.toString('base64'))

  await page.getByTestId('image-dropzone').dispatchEvent('drop', { dataTransfer: transfer })
  await expect(page.getByTestId('image-item')).toHaveCount(1, { timeout: 60_000 })

  // Held back on its way to storage, for long enough to see the upload in progress.
  await page.route(/\/incoming\//, async (route) => {
    await new Promise((resolve) => setTimeout(resolve, 1500))
    await route.continue()
  })
  await page.getByTestId('image-input').setInputFiles({
    name: 'second.png',
    mimeType: 'image/png',
    buffer: png,
  })
  await expect(page.getByTestId('upload-progress')).toBeVisible()
  // Nothing can be saved while an image is still on its way.
  await expect(page.getByTestId('product-save')).toBeDisabled()
  await expect(page.getByTestId('image-item')).toHaveCount(2, { timeout: 60_000 })
  await expect(page.getByTestId('product-save')).toBeEnabled()

  const altInputs = page.getByTestId('image-item').locator('input')

  await altInputs.nth(0).fill('Birinji')
  await altInputs.nth(1).fill('Ikinji')
  await page.getByTestId('image-item').nth(1).getByTestId('image-earlier').click()
  await expect(altInputs.nth(0)).toHaveValue('Ikinji')
  await expect(altInputs.nth(1)).toHaveValue('Birinji')

  let order = ['Ikinji', 'Birinji']

  // Dragging is for a mouse. The arrows above are how a phone does the same.
  if (testInfo.project.name !== 'phone') {
    const items = page.getByTestId('image-item')

    await items.nth(1).locator('img').dragTo(items.nth(0).locator('img'))
    await expect(altInputs.nth(0)).toHaveValue('Birinji')
    order = ['Birinji', 'Ikinji']
  }

  await chooseTab(page, 'Tekstler')
  await page.getByTestId('name-tk').fill('Suratly haryt')
  await chooseTab(page, 'Esasy')
  await page.getByTestId('product-slug').fill(unique('suratly'))
  await chooseTab(page, 'Wariantlar')
  await page.getByTestId('variant-sku').fill(unique('SUR'))
  await page.getByTestId('variant-price').fill('10')
  await page.getByTestId('product-save').click()
  await expect(page).toHaveURL(/\/tk\/products\/[0-9a-f-]{36}$/)

  const api = await adminApi(token.current())
  const saved = await ok<ProductDto>(
    await api.get(`/api/v1/admin/catalog/products/${page.url().split('/').pop()}`),
  )

  expect(saved.images.map((image) => image.altText)).toEqual(order)
  await api.dispose()
})

test.describe('pasting hostile text', () => {
  // The pasted handler is meant to be stopped: the policy blocks it, and then the editor drops it.
  test.use({ expectedProblems: /inline event handler/ })

  test('the editor cannot save what the sanitizer list does not allow', async ({ page }) => {
    test.slow()

    const token = watchToken(page)

    await signInAsNewOwner(page, 'Redaktor Dükany')
    await page.goto(`${ADMIN_URL}/tk/products/new`)
    await chooseTab(page, 'Tekstler')
    await page.getByTestId('name-tk').fill('Arassa haryt')

    const editor = visibleEditor(page)

    await expect(editor).toBeVisible()
    await editor.click()

    // The way hostile content really arrives: pasted from another page.
    await editor.evaluate((element) => {
      const data = new DataTransfer()

      data.setData(
        'text/html',
        `<h1 onclick="alert(1)">Sözbaşy</h1><p>Tekst</p><script>alert(2)</script>
       <img src="x" onerror="alert(3)"><hr><pre><code>kod</code></pre>
       <a href="javascript:alert(4)">baglanyşyk</a>`,
      )
      element.dispatchEvent(
        new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }),
      )
    })
    await expect(editor).toContainText('Tekst')

    const shown = await editor.innerHTML()

    expect(shown).not.toMatch(/onclick|onerror|<script|<img|<hr|<pre|<code|javascript:|<h1/)

    await chooseTab(page, 'Esasy')
    await page.getByTestId('product-slug').fill(unique('arassa'))
    await chooseTab(page, 'Wariantlar')
    await page.getByTestId('variant-sku').fill(unique('ARA'))
    await page.getByTestId('variant-price').fill('10')
    await page.getByTestId('product-save').click()
    await expect(page).toHaveURL(/\/tk\/products\/[0-9a-f-]{36}$/)

    const api = await adminApi(token.current())
    const saved = await ok<ProductDto>(
      await api.get(`/api/v1/admin/catalog/products/${page.url().split('/').pop()}`),
    )

    expect(saved.translations[0]!.description).not.toMatch(
      /onclick|onerror|<script|<img|javascript:/,
    )
    await api.dispose()
  })
})

test('a link goes into a description, and typing carries on where it was', async ({ page }) => {
  test.slow()

  const token = watchToken(page)

  await signInAsNewOwner(page, 'Baglanyşyk Dükany')
  await page.goto(`${ADMIN_URL}/tk/products/new`)
  await chooseTab(page, 'Tekstler')
  await page.getByTestId('name-tk').fill('Baglanyşykly haryt')

  const editor = visibleEditor(page)

  await expect(editor).toBeVisible()
  await editor.click()
  await page.keyboard.type('sahypa')
  await page.keyboard.press('ControlOrMeta+a')
  await page.getByTestId('editor-link').locator('visible=true').click()

  const prompt = page.getByRole('dialog').filter({ hasText: 'Salgyny giriziň' })

  // Something that could run code is turned away by the prompt itself.
  await prompt.locator('input').fill('javascript:alert(1)')
  await prompt.getByRole('button', { name: 'Tassykla' }).click()
  await expect(prompt).toContainText('http:// ýa-da https://')

  await prompt.locator('input').fill('https://example.com/kitap')
  await prompt.getByRole('button', { name: 'Tassykla' }).click()
  await expect(editor.locator('a')).toHaveAttribute('href', 'https://example.com/kitap')

  // With a link in the text, each keystroke must leave the cursor where it was.
  await editor.click()
  await page.keyboard.press('ControlOrMeta+End')
  await page.keyboard.type(' we dowamy bar')
  await expect(editor).toContainText('sahypa we dowamy bar')

  await chooseTab(page, 'Esasy')
  await page.getByTestId('product-slug').fill(unique('baglanysyk'))
  await chooseTab(page, 'Wariantlar')
  await page.getByTestId('variant-sku').fill(unique('LNK'))
  await page.getByTestId('variant-price').fill('10')
  await page.getByTestId('product-save').click()
  await expect(page).toHaveURL(/\/tk\/products\/[0-9a-f-]{36}$/)

  const api = await adminApi(token.current())
  const saved = await ok<ProductDto>(
    await api.get(`/api/v1/admin/catalog/products/${page.url().split('/').pop()}`),
  )

  expect(saved.translations[0]!.description).toContain('href="https://example.com/kitap"')
  await api.dispose()
})

test('leaving a form with unsaved changes asks first', async ({ page }) => {
  await signInAsNewOwner(page, 'Duýduryş Dükany')
  await page.goto(`${ADMIN_URL}/tk/products/new`)
  await page.getByTestId('product-slug').fill('saklanmadyk')

  await openFromMenu(page, 'orders')

  const question = page.getByRole('dialog').filter({ hasText: 'Saklanmadyk üýtgeşmeler' })

  await expect(question).toBeVisible()
  await question.getByRole('button', { name: 'Sahypada gal' }).click()
  await expect(page).toHaveURL(/\/products\/new$/)
  await expect(page.getByTestId('product-slug')).toHaveValue('saklanmadyk')

  await openFromMenu(page, 'orders')
  await expect(question).toBeVisible()
  await question.getByRole('button', { name: 'Saklamazdan çyk' }).click()
  await expect(page).toHaveURL(/\/tk\/orders$/)

  // Closing the tab is the browser's to ask about, and only while there is something to lose.
  await page.goto(`${ADMIN_URL}/tk/products/new`)
  await page.getByTestId('product-slug').fill('yene-saklanmadyk')

  const asked = page.waitForEvent('dialog')

  await page.close({ runBeforeUnload: true })

  const dialog = await asked

  expect(dialog.type()).toBe('beforeunload')
  await dialog.accept()
})

test('saving over a colleague’s change is refused and explained', async ({ page }) => {
  test.slow()

  const token = watchToken(page)

  await signInAsNewOwner(page, 'Çaknyşyk Dükany')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  const api = await adminApi(token.current())
  const product = await createProduct(api, { name: 'Ilkinji ady', publish: false })

  await page.goto(`${ADMIN_URL}/tk/products/${product.id}`)
  await expect(page.getByTestId('product-slug')).toBeVisible()

  // A colleague saves first, which moves the version on.
  const current = await ok<ProductDto>(
    await api.get(`/api/v1/admin/catalog/products/${product.id}`),
  )

  await ok(
    await api.put(`/api/v1/admin/catalog/products/${product.id}`, {
      data: {
        slug: current.slug,
        translations: [{ language: 'tk', name: 'Kärdeşiň ady' }],
        variants: current.variants.map((variant) => ({
          ...variant,
          currency: 'TMT',
          isActive: true,
        })),
        version: current.version,
      },
    }),
  )

  await chooseTab(page, 'Tekstler')
  await page.getByTestId('name-tk').fill('Meniň adym')
  await page.getByTestId('product-save').click()

  const conflict = page.getByTestId('conflict')

  await expect(conflict).toContainText('başga biri tarapyndan üýtgedildi')

  await page.getByTestId('conflict-reload').click()
  await expect(conflict).toHaveCount(0)
  await chooseTab(page, 'Tekstler')
  await expect(page.getByTestId('name-tk')).toHaveValue('Kärdeşiň ady')

  // Now on the current version, a save goes through.
  await page.getByTestId('name-tk').fill('Meniň adym')
  await page.getByTestId('product-save').click()
  await expect(page.getByText('Haryt saklandy')).toBeVisible()
  await expect(conflict).toHaveCount(0)
  await api.dispose()
})

test('a changed price shows up in the price history', async ({ page }) => {
  test.slow()

  const token = watchToken(page)

  await signInAsNewOwner(page, 'Baha Dükany')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  const api = await adminApi(token.current())
  const product = await createProduct(api, { price: 100, publish: false })

  await page.goto(`${ADMIN_URL}/tk/products/${product.id}`)
  await chooseTab(page, 'Wariantlar')
  await page.getByTestId('variant-price').fill('125')
  await page.getByTestId('product-save').click()
  await expect(page.getByText('Haryt saklandy')).toBeVisible()

  await chooseTab(page, 'Baha taryhy')

  const history = page.getByRole('tabpanel').locator('table').last()

  await expect(history).toContainText(product.sku)
  await expect(history).toContainText('100,00')
  await expect(history).toContainText('125,00')
  await api.dispose()
})

test('bulk actions work on the chosen products, and deleting asks first', async ({ page }) => {
  test.slow()

  const token = watchToken(page)

  await signInAsNewOwner(page, 'Köpçülik Dükany')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  const api = await adminApi(token.current())
  const first = await createProduct(api, { name: 'Birinji haryt', publish: false })
  const second = await createProduct(api, { name: 'Ikinji haryt', publish: false })

  await createProduct(api, { name: 'Üçünji haryt', publish: false })

  await page.goto(`${ADMIN_URL}/tk/products`)

  const rowOf = (name: string) => page.getByRole('row').filter({ hasText: name })

  await rowOf('Birinji haryt').locator('.el-checkbox').click()
  await rowOf('Ikinji haryt').locator('.el-checkbox').click()
  await expect(page.getByTestId('bulk-bar')).toContainText('2 saýlandy')

  await page.getByTestId('bulk-publish').click()

  const dialog = page.getByRole('dialog')

  await expect(dialog).toContainText('2 harydy çap etmelimi')
  await dialog.getByRole('button', { name: 'Tassykla' }).click()
  await expect(page.getByText('2 haryt üçin ýerine ýetirildi')).toBeVisible()
  await expect(rowOf('Birinji haryt')).toContainText('Çap edilen')
  await expect(rowOf('Ikinji haryt')).toContainText('Çap edilen')
  await expect(rowOf('Üçünji haryt')).toContainText('Garalama')
  await expect(page.getByTestId('bulk-bar')).toHaveCount(0)

  const published = await ok<ProductDto>(
    await api.get(`/api/v1/admin/catalog/products/${first.id}`),
  )

  expect(published.status).toBe('published')

  await rowOf('Birinji haryt').locator('.el-checkbox').click()
  await page.getByTestId('bulk-archive').click()
  await dialog.getByRole('button', { name: 'Tassykla' }).click()
  await expect(rowOf('Birinji haryt')).toContainText('Arhiw')

  // Saying no leaves the product alone.
  await rowOf('Ikinji haryt').locator('.el-checkbox').click()
  await page.getByTestId('bulk-delete').click()
  await expect(dialog).toContainText('yzyna alyp bolmaýar')
  await dialog.getByRole('button', { name: 'Ýatyr' }).click()
  await expect(dialog).toHaveCount(0)
  await expect(rowOf('Ikinji haryt')).toBeVisible()

  await page.getByTestId('bulk-delete').click()
  await dialog.getByRole('button', { name: 'Poz' }).click()
  await expect(rowOf('Ikinji haryt')).toHaveCount(0)

  expect((await api.get(`/api/v1/admin/catalog/products/${second.id}`)).status()).toBe(404)
  await api.dispose()
})

test('the list keeps its filter in the address and remembers the chosen columns', async ({
  page,
}) => {
  test.slow()

  const token = watchToken(page)

  await signInAsNewOwner(page, 'Süzgüç Dükany')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  const api = await adminApi(token.current())

  await createProduct(api, { name: 'Çap edilen haryt' })
  await createProduct(api, { name: 'Garalama haryt', publish: false })
  await page.goto(`${ADMIN_URL}/tk/products`)

  await choose(page, 'filter-status', 'Garalama')
  await expect(page).toHaveURL(/status=draft/)
  await expect(page.getByTestId('product-link')).toHaveCount(1)
  await expect(page.getByTestId('product-link')).toHaveText('Garalama haryt')

  // A copied link or a reload gives the same list.
  await page.reload()
  await expect(page.getByTestId('product-link')).toHaveText('Garalama haryt')

  await page.getByRole('button', { name: 'Süzgüçleri arassala' }).click()
  await expect(page.getByTestId('product-link')).toHaveCount(2)

  // The search covers drafts as well, ignores case, and keeps its term in the address too.
  await page.getByTestId('filter-search').fill('GARALAMA')
  await page.getByTestId('filter-search').press('Enter')
  await expect(page).toHaveURL(/q=GARALAMA/)
  // The old rows stay on screen until the answer arrives, and toHaveText refuses two rows outright.
  await expect(page.getByTestId('product-link')).toHaveCount(1)
  await expect(page.getByTestId('product-link')).toHaveText('Garalama haryt')
  await page.reload()
  await expect(page.getByTestId('filter-search')).toHaveValue('GARALAMA')
  await expect(page.getByTestId('product-link')).toHaveText('Garalama haryt')

  await page.getByTestId('filter-search').fill('hiç-ýerde-ýok')
  await page.getByTestId('filter-search').press('Enter')
  await expect(page.getByText('Bu süzgüçlere laýyk haryt ýok')).toBeVisible()

  await page.getByRole('button', { name: 'Süzgüçleri arassala' }).click()
  await expect(page).not.toHaveURL(/status=/)
  await expect(page.getByTestId('product-link')).toHaveCount(2)

  // Sorting goes to the server through the address as well.
  await page.getByRole('columnheader', { name: 'Salgy (slug)' }).click()
  await expect(page).toHaveURL(/sort=slug/)

  await expect(page.getByRole('columnheader', { name: 'Wariantlar' })).toBeVisible()
  await page.getByTestId('column-chooser').click()
  await page.getByTestId('column-variantCount').click()
  await expect(page.getByRole('columnheader', { name: 'Wariantlar' })).toHaveCount(0)

  await page.reload()
  await expect(page.getByTestId('product-link').first()).toBeVisible()
  await expect(page.getByRole('columnheader', { name: 'Wariantlar' })).toHaveCount(0)
  await expect(page).toHaveURL(/sort=slug/)
  await api.dispose()
})

test('a long list pages through the server, and the back button goes back a page', async ({
  page,
}) => {
  test.slow()

  const token = watchToken(page)

  await signInAsNewOwner(page, 'Sahypa Dükany')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  const api = await adminApi(token.current())

  await Promise.all(
    Array.from({ length: 23 }, (_, index) =>
      createProduct(api, { name: `Haryt ${index}`, publish: false }),
    ),
  )

  await page.goto(`${ADMIN_URL}/tk/products`)

  const links = page.getByTestId('product-link')

  await expect(links).toHaveCount(20)
  await expect(page.locator('.el-pagination__total')).toContainText('23')

  await page.locator('.el-pager li').filter({ hasText: /^2$/ }).click()
  await expect(page).toHaveURL(/page=2/)
  await expect(links).toHaveCount(3)

  await page.goBack()
  await expect(page).not.toHaveURL(/page=2/)
  await expect(links).toHaveCount(20)

  await page.goto(`${ADMIN_URL}/tk/products?pageSize=50`)
  await expect(links).toHaveCount(23)

  // An address past the end shows that, with a way back, and does not pretend there is nothing.
  await page.goto(`${ADMIN_URL}/tk/products?page=9`)
  await expect(page.getByText('Entek haryt ýok')).toBeVisible()
  await expect(page.locator('.el-pagination__total')).toContainText('23')
  await api.dispose()
})
