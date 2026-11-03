import { setUpTwoFactor, signIn, signInAsNewOwner } from './support/admin'
import { acceptInvitation, adminApi, choose, createProduct, watchToken } from './support/admin-data'
import { expect, test } from './support/fixtures'
import { ADMIN_URL, createConfirmedUser } from './support/stack'

test('staff are invited, see only what their role allows, and can be changed and removed', async ({
  page,
  browser,
}) => {
  test.setTimeout(180_000)

  const token = watchToken(page)

  await signInAsNewOwner(page, 'Işgär Dükany')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()

  const api = await adminApi(token.current())
  const product = await createProduct(api, { name: 'Görülýän haryt' })

  await page.goto(`${ADMIN_URL}/tk/staff`)

  // The matrix is there for reference, and nothing in it can be clicked to change anything.
  await expect(page.getByTestId('staff-row')).toHaveCount(1)
  await expect(page.locator('table').last().getByRole('checkbox')).toHaveCount(0)

  const email = await createConfirmedUser('Täze Işgär')

  await page.getByTestId('invite-staff').click()
  await page.getByTestId('invite-email').fill(email)
  await choose(page, 'invite-role', 'Işgär')
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByTestId('invitation-row')).toHaveText(email)

  // An invitation can be taken back, and that also asks first.
  const secondEmail = await createConfirmedUser('Ikinji Işgär')

  await page.getByTestId('invite-staff').click()
  await page.getByTestId('invite-email').fill(secondEmail)
  await page.getByTestId('drawer-submit').click()
  await expect(page.getByTestId('invitation-row')).toHaveCount(2)
  await page
    .getByRole('row')
    .filter({ hasText: secondEmail })
    .getByTestId('invitation-revoke')
    .click()
  await page.getByRole('dialog').getByRole('button', { name: 'Yzyna al' }).click()
  await expect(page.getByTestId('invitation-row')).toHaveCount(1)

  await acceptInvitation(email)

  const staffContext = await browser.newContext()
  const staffPage = await staffContext.newPage()

  await signIn(staffPage, email)
  await setUpTwoFactor(staffPage)
  await expect(staffPage).toHaveURL(/\/tk(\/select-store)?$/)
  await expect(staffPage.getByRole('heading', { name: /Hoş geldiňiz, Täze Işgär/ })).toBeVisible()

  const menu = staffPage.getByTestId('side-menu').locator('visible=true')

  if (await staffPage.getByTestId('menu-button').isVisible()) {
    await staffPage.getByTestId('menu-button').click()
  }

  await expect(menu.getByTestId('nav-products')).toBeVisible()
  await expect(menu.getByTestId('nav-orders')).toBeVisible()
  await expect(menu.getByTestId('nav-customers')).toBeVisible()
  await expect(menu.getByTestId('nav-settings')).toHaveCount(0)
  await expect(menu.getByTestId('nav-staff')).toHaveCount(0)
  await expect(menu.getByTestId('nav-tenants')).toHaveCount(0)

  // Typing the address does not help, and the guard stops it before the page loads anything.
  await staffPage.goto(`${ADMIN_URL}/tk/settings`)
  await expect(staffPage).toHaveURL(/\/tk\/forbidden$/)
  await expect(staffPage.getByRole('heading', { name: 'Rugsat ýok' })).toBeVisible()
  await staffPage.goto(`${ADMIN_URL}/tk/products/new`)
  await expect(staffPage).toHaveURL(/\/tk\/forbidden$/)

  // Staff may read the catalog but not change it.
  await staffPage.goto(`${ADMIN_URL}/tk/products`)
  await expect(staffPage.getByRole('heading', { name: 'Harytlar' })).toBeVisible()
  await expect(staffPage.getByTestId('new-product')).toHaveCount(0)

  // The form opens to read: every input is off, and there is nothing to save with.
  await staffPage.goto(`${ADMIN_URL}/tk/products/${product.id}`)
  await expect(staffPage.getByTestId('product-slug')).toBeDisabled()
  await expect(staffPage.getByTestId('product-save')).toHaveCount(0)
  await expect(staffPage.getByTestId('product-delete')).toHaveCount(0)

  await staffContext.close()

  // Back as the owner: the new member has arrived.
  await page.reload()

  const row = page.getByRole('row').filter({ hasText: 'Täze Işgär' })

  await expect(row).toContainText('Işgär')
  await expect(
    page.getByRole('row').filter({ hasText: 'E2E Eýe' }).getByTestId('staff-remove'),
  ).toHaveCount(0)

  await row.getByTestId('staff-role').click()
  await choose(page, 'staff-role-select', 'Dolandyryjy')
  await page.getByRole('dialog').getByRole('button', { name: 'Tassykla' }).click()
  await expect(row).toContainText('Dolandyryjy')

  await row.getByTestId('staff-remove').click()

  const question = page.getByRole('dialog').filter({ hasText: 'aýrylsynmy' })

  await expect(question).toContainText('Täze Işgär')
  await question.getByRole('button', { name: 'Aýyr' }).click()
  await expect(row).toHaveCount(0)
  await api.dispose()
})

test('an owner cannot reach platform pages', async ({ page }) => {
  const token = watchToken(page)

  await signInAsNewOwner(page, 'Eýe Dükany')
  await expect(page.getByTestId('dashboard-tiles')).toBeVisible()
  await page.goto(`${ADMIN_URL}/tk/platform/tenants`)
  await expect(page).toHaveURL(/\/tk\/forbidden$/)

  // The server is the real check, and it agrees.
  const api = await adminApi(token.current())

  expect((await api.get('/api/v1/platform/tenants')).status()).toBe(403)
  await api.dispose()
})
