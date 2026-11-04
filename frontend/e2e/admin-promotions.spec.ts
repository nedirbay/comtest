import { signInAsNewOwner } from './support/admin'
import { addStaffMember, adminApi, watchToken } from './support/admin-data'
import { expect, test } from './support/fixtures'
import { ADMIN_URL } from './support/stack'

test('promotions are made, changed and removed in the panel', async ({ page }) => {
  await signInAsNewOwner(page, 'Aksiýa Dükany')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  await page.goto(`${ADMIN_URL}/tk/promotions`)
  await expect(page.getByRole('heading', { name: 'Aksiýalar' })).toBeVisible()
  await expect(page.locator('main')).toContainText('Entek aksiýa ýok')

  await page.getByTestId('new-promotion').click()
  await page.getByTestId('promotion-name').fill('Baýram')

  // The form explains what it will not take before anything goes to the server.
  await page.getByTestId('promotion-code').fill('KISA')
  await page.getByTestId('promotion-value').fill('150')
  await page.getByRole('button', { name: 'Ýatda sakla' }).click()
  await expect(page.getByText('Kod 6–32 belgiden ybarat bolmaly')).toBeVisible()
  await expect(page.getByText('Göterim 100-den köp bolup bilmez.')).toBeVisible()

  await page.getByTestId('promotion-generate').click()
  await expect(page.getByTestId('promotion-code')).toHaveValue(/^[A-Z2-9]{12}$/)

  const code = await page.getByTestId('promotion-code').inputValue()

  await page.getByTestId('promotion-value').fill('15')
  await page.getByRole('button', { name: 'Ýatda sakla' }).click()

  await expect(page.getByTestId('promotion-row')).toHaveText('Baýram')
  await expect(page.locator('main')).toContainText(code)
  await expect(page.locator('main')).toContainText('15%')
  await expect(page.getByTestId('promotion-usage')).toHaveText('0')

  await page.getByTestId('promotion-edit').click()
  await expect(page.getByTestId('promotion-code')).toHaveValue(code)
  await page.getByTestId('promotion-automatic').click()
  await page.getByRole('button', { name: 'Ýatda sakla' }).click()
  await expect(page.locator('main')).toContainText('Awtomatik')

  await page.getByTestId('promotion-delete').click()
  await page.getByRole('dialog').getByRole('button', { name: 'Poz' }).click()
  await expect(page.locator('main')).toContainText('Entek aksiýa ýok')
})

test('staff cannot reach promotions', async ({ page, browser }) => {
  const token = watchToken(page)

  await signInAsNewOwner(page, 'Aksiýa Işgäri')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  const api = await adminApi(token.current())
  const staff = await addStaffMember(api, browser, 'Staff')

  await expect(staff.page.getByTestId('nav-promotions')).toHaveCount(0)
  await staff.page.goto(`${ADMIN_URL}/tk/promotions`)
  await expect(staff.page).toHaveURL(/\/forbidden/)

  await staff.context.close()
  await api.dispose()
})
