import { expect, test } from './support/fixtures'
import { inviteStaffToNewStore } from './support/demo-store'
import { createConfirmedUser, linkFromMail, PASSWORD, STOREFRONT_URL } from './support/stack'

test('an invited employee signs in from the mail link and joins the store', async ({ page }) => {
  test.slow()

  const email = await createConfirmedUser('Täze Işgär')

  await inviteStaffToNewStore(email)
  await page.goto(await linkFromMail(email, 'accept-invitation'))

  // The token must not stay in the address bar, and has to survive the sign-in page.
  await expect(page.getByTestId('invitation-login')).toBeVisible()
  expect(page.url()).not.toContain('token')

  await page.getByTestId('invitation-login').getByRole('link', { name: 'Girmek' }).click()
  await page.getByLabel('E-poçta ýa-da telefon').fill(email)
  await page.locator('input[type="password"]').fill(PASSWORD)
  await page.getByRole('button', { name: 'Girmek', exact: true }).click()

  await expect(page.getByText('Çakylyk kabul edildi')).toBeVisible()
  await expect(page.getByTestId('open-admin')).toBeVisible()
})

test('the invitation page without a token explains itself', async ({ page }) => {
  await page.goto(`${STOREFRONT_URL}/accept-invitation`)

  await expect(page.getByText('Bu salgy doly däl')).toBeVisible()
})
