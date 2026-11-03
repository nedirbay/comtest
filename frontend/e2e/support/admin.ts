import { expect, type Page } from '@playwright/test'
import { ADMIN_URL, createStoreOwner, enterOtp, PASSWORD, totp } from './stack'

export async function signIn(page: Page, email: string, password = PASSWORD): Promise<void> {
  await page.goto(`${ADMIN_URL}/tk/login`)
  await page.getByLabel('E-poçta ýa-da telefon').fill(email)
  await page.locator('input[type="password"]').fill(password)
  await page.getByRole('button', { name: 'Girmek', exact: true }).click()
}

/** Takes a new owner through the forced setup and returns the key for later sign-ins. */
export async function setUpTwoFactor(
  page: Page,
): Promise<{ sharedKey: string; usedAt: number; codes: string[] }> {
  await expect(page).toHaveURL(/two-factor-setup/)
  await expect(page.getByTestId('qr')).toBeVisible()

  const sharedKey = (await page.getByTestId('shared-key').innerText()).trim()
  const usedAt = Date.now()

  await enterOtp(page, totp(sharedKey, usedAt))
  await page.getByRole('button', { name: 'Işjeňleşdir' }).click()

  const list = page.getByTestId('recovery-codes')

  await expect(list).toBeVisible()

  const codes = await list.locator('li').allInnerTexts()

  await expect(page.getByRole('button', { name: 'Dowam et' })).toBeDisabled()
  await page.getByText('Kodlary ygtybarly ýerde sakladym').click()
  await page.getByRole('button', { name: 'Dowam et' }).click()

  return { sharedKey, usedAt, codes }
}

export async function logout(page: Page): Promise<void> {
  await page.getByTestId('user-menu').click()
  await page.getByTestId('logout').click()
  await expect(page).toHaveURL(/\/login/)
}

/** A new firm whose owner is signed in and past the forced two-factor setup. */
export async function signInAsNewOwner(page: Page, storeName = 'E2E Dükany') {
  const owner = await createStoreOwner(storeName)

  await signIn(page, owner.email)

  return { ...owner, ...(await setUpTwoFactor(page)) }
}
