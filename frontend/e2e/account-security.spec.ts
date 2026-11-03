import { readFile } from 'node:fs/promises'
import type { Page } from '@playwright/test'
import { expect, test } from './support/fixtures'
import {
  createConfirmedUser,
  enterOtp,
  nextTotp,
  PASSWORD,
  STOREFRONT_URL,
  totp,
} from './support/stack'

const NEW_PASSWORD = 'a-completely-different-passphrase'

async function signIn(page: Page, email: string, password = PASSWORD): Promise<void> {
  await page.goto(`${STOREFRONT_URL}/login?returnUrl=/account/security`)
  await page.getByLabel('E-poçta ýa-da telefon').fill(email)
  await page.locator('input[type="password"]').fill(password)
  await page.getByRole('button', { name: 'Girmek', exact: true }).click()
}

test('a customer can change their password and keep working', async ({ page }) => {
  const email = await createConfirmedUser()

  await signIn(page, email)
  await expect(page).toHaveURL(/account\/security/)

  await page.getByLabel('Häzirki açar sözi').fill(PASSWORD)
  await page.getByLabel('Täze açar sözi', { exact: true }).fill(NEW_PASSWORD)
  await page.getByLabel('Täze açar sözüni gaýtalaň').fill(NEW_PASSWORD)
  await page.getByRole('button', { name: 'Açar sözüni üýtget' }).click()
  await expect(page.getByText('Açar sözi üýtgedildi')).toBeVisible()

  // The change retires the old token. The next request has to renew it without any fuss.
  await page.reload()
  await expect(page.getByTestId('session').first()).toBeVisible()

  await page.goto(`${STOREFRONT_URL}/account`)
  await page.getByTestId('logout').click()
  await expect(page).toHaveURL(`${STOREFRONT_URL}/`)

  await signIn(page, email)
  await expect(page.getByRole('alert')).toBeVisible()

  await signIn(page, email, NEW_PASSWORD)
  await expect(page).toHaveURL(/account\/security/)
})

test('the wrong current password is refused', async ({ page }) => {
  const email = await createConfirmedUser()

  await signIn(page, email)
  await page.getByLabel('Häzirki açar sözi').fill('not-my-current-password')
  await page.getByLabel('Täze açar sözi', { exact: true }).fill(NEW_PASSWORD)
  await page.getByLabel('Täze açar sözüni gaýtalaň').fill(NEW_PASSWORD)
  await page.getByRole('button', { name: 'Açar sözüni üýtget' }).click()

  await expect(page.getByRole('alert').filter({ hasNotText: 'üýtgedildi' })).toBeVisible()
  await expect(page.getByText('Açar sözi üýtgedildi')).toBeHidden()
})

test('a customer can turn two-factor on, renew the codes and turn it off', async ({ page }) => {
  test.slow()

  const email = await createConfirmedUser()

  await signIn(page, email)
  await page.getByRole('button', { name: 'Işjeňleşdir' }).click()

  const dialog = page.getByRole('dialog')

  await expect(dialog.getByTestId('qr')).toBeVisible()

  const sharedKey = (await dialog.getByTestId('shared-key').innerText()).trim()
  let usedAt = Date.now()

  await enterOtp(page, totp(sharedKey, usedAt))
  await dialog.getByRole('button', { name: 'Işjeňleşdir' }).click()

  const codeList = dialog.getByTestId('recovery-codes').locator('li')

  await expect(codeList.first()).toBeVisible()

  const firstCodes = await codeList.allInnerTexts()

  expect(firstCodes.length).toBeGreaterThanOrEqual(8)

  await dialog.getByText('Kodlary ygtybarly ýerde sakladym').click()
  await dialog.getByRole('button', { name: 'Dowam et' }).click()
  await expect(page.getByText('Işjeň. Her girişde')).toBeVisible()

  await page.getByRole('button', { name: 'Täze dikeldiş kodlary' }).click()

  let next = await nextTotp(sharedKey, usedAt)

  usedAt = next.at
  await enterOtp(page, next.code)
  await dialog.getByRole('button', { name: 'Tassykla' }).click()

  await expect(codeList.first()).toBeVisible()
  expect(await codeList.allInnerTexts()).not.toEqual(firstCodes)
  await dialog.getByText('Kodlary ygtybarly ýerde sakladym').click()
  await dialog.getByRole('button', { name: 'Dowam et' }).click()

  await page.getByRole('button', { name: 'Öçür' }).click()
  await dialog.locator('input[type="password"]').fill(PASSWORD)
  next = await nextTotp(sharedKey, usedAt)
  await enterOtp(page, next.code)
  await dialog.getByRole('button', { name: 'Öçür' }).click()

  await expect(page.getByText('Işjeň däl.')).toBeVisible()
})

test('a customer can confirm a phone number with an SMS code', async ({ page }, testInfo) => {
  // The code is read from the API log, where two parallel phone tests could not be told apart.
  test.skip(testInfo.project.name !== 'desktop')

  const email = await createConfirmedUser()
  const phone = `+9936${String(Date.now()).slice(-7)}`

  await signIn(page, email)
  await page.getByLabel('Telefon belgisi').fill(phone)
  await page.getByRole('button', { name: 'Kod ibermek' }).click()
  await expect(page.getByText('belgisine kod iberildi')).toBeVisible()

  let code: string | undefined

  await expect
    .poll(async () => {
      const log = await readFile('.e2e/api.log', 'utf8')
      const lines = log.split('\n').filter((line) => line.includes('Development SMS'))

      code = /\b\d{6}\b/.exec(lines.at(-1) ?? '')?.[0]

      return code
    })
    .toBeTruthy()

  await enterOtp(page, code!)
  await page.getByRole('button', { name: 'Tassykla' }).click()

  await expect(page.getByText('Tassyklanan belgi:')).toBeVisible()
})
