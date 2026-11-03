import { expect, test } from './support/fixtures'

/** This server's API address is a closed port. */
const SERVER = 'http://localhost:3003'

test('the store shows a message when the API is down, and does not crash', async ({ page }) => {
  const response = await page.goto(SERVER)

  expect(response?.status()).toBe(200)
  // Right after loading as well as after hydration, or the page would break for a moment. The page
  // health check also fails this test on a hydration mismatch.
  await expect(page.getByText('Dükan häzir elýeterli däl')).toBeVisible()
  await page.waitForLoadState('networkidle')
  await expect(page.getByText('Dükan häzir elýeterli däl')).toBeVisible()
})

test('signing in without a connection says so in plain words', async ({ page }) => {
  await page.goto(`${SERVER}/login`)
  await page.getByLabel('E-poçta ýa-da telefon').fill('ayna@example.tm')
  await page.locator('input[type="password"]').fill('any-password-at-all')
  await page.getByRole('button', { name: 'Girmek', exact: true }).click()

  await expect(page.getByRole('alert')).toHaveText(/Serwer bilen baglanyşyk ýok/)
})
