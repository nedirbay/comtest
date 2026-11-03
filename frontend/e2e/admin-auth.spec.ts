import { expect, test } from './support/fixtures'
import { logout, setUpTwoFactor, signIn } from './support/admin'
import {
  ADMIN_URL,
  createConfirmedUser,
  createStoreOwner,
  enterOtp,
  expectNoTokensInBrowserStorage,
  nextTotp,
} from './support/stack'

test('the admin panel is hidden from search engines', async ({ request }) => {
  const html = await (await request.get(`${ADMIN_URL}/tk/login`)).text()

  expect(html).toContain('noindex')
})

test('a guest is sent to sign-in, and to the original page afterwards', async ({ page }) => {
  await page.goto(`${ADMIN_URL}/ru/security`)

  await expect(page).toHaveURL(/\/ru\/login\?returnUrl=/)
  await expect(page.getByRole('heading', { name: 'Вход в систему' })).toBeVisible()
})

test('a new owner must set up two-factor, then signs in with it', async ({ page }) => {
  test.slow()

  const { email } = await createStoreOwner()

  await signIn(page, email)

  const { sharedKey, usedAt, codes } = await setUpTwoFactor(page)

  expect(codes.length).toBeGreaterThanOrEqual(8)

  await expect(page).toHaveURL(`${ADMIN_URL}/tk`)
  await expect(page.getByRole('heading', { name: /Hoş geldiňiz, E2E Eýe/ })).toBeVisible()
  await expectNoTokensInBrowserStorage(page)

  // A reload starts from nothing but the cookie and has to keep the store and the second factor.
  await page.reload()
  await expect(page.getByRole('heading', { name: /Hoş geldiňiz, E2E Eýe/ })).toBeVisible()

  await logout(page)
  await page.goto(`${ADMIN_URL}/tk`)
  await expect(page).toHaveURL(/\/login/)

  await signIn(page, email)
  await expect(page.getByText('Iki basgançakly tassyklama')).toBeVisible()
  await enterOtp(page, (await nextTotp(sharedKey, usedAt)).code)
  await expect(page).toHaveURL(`${ADMIN_URL}/tk`)
  await logout(page)

  // Recovery codes work when the phone is gone.
  await signIn(page, email)
  await page.getByRole('button', { name: 'Dikeldiş koduny ulan' }).click()
  await page.getByLabel('Dikeldiş kody').fill(codes[0]!)
  await page.getByRole('button', { name: 'Tassykla' }).click()
  await expect(page).toHaveURL(`${ADMIN_URL}/tk`)
})

test('a wrong two-factor code is refused', async ({ page }) => {
  test.slow()

  const { email } = await createStoreOwner()

  await signIn(page, email)
  await setUpTwoFactor(page)
  await logout(page)

  await signIn(page, email)
  await enterOtp(page, '000000')
  await expect(page.getByRole('alert')).toBeVisible()
  await expect(page).toHaveURL(/\/login/)
})

test('a customer without a store is told so, and gets no admin pages', async ({ page }) => {
  const email = await createConfirmedUser()

  await signIn(page, email)
  await expect(page).toHaveURL(/select-store/)
  await expect(page.getByText('Hasabyňyz entek hiç bir dükana bellenmedik')).toBeVisible()

  await page.goto(`${ADMIN_URL}/tk/security`)
  await expect(page).toHaveURL(/select-store/)
})

test('signing out in one tab signs out the others', async ({ page, context }) => {
  test.slow()

  const { email } = await createStoreOwner()

  await signIn(page, email)
  await setUpTwoFactor(page)

  const second = await context.newPage()

  await second.goto(`${ADMIN_URL}/tk/security`)
  await expect(second.getByRole('heading', { name: 'Howpsuzlyk' })).toBeVisible()
  await expect(second.getByTestId('session')).toHaveCount(1)

  await logout(page)
  await expect(second).toHaveURL(/\/login/)
})

test('the sessions page lists this device and can close another one', async ({ page, browser }) => {
  test.slow()

  const { email } = await createStoreOwner()

  await signIn(page, email)

  const { sharedKey, usedAt } = await setUpTwoFactor(page)
  const other = await browser.newContext()
  const otherPage = await other.newPage()

  await signIn(otherPage, email)
  await enterOtp(otherPage, (await nextTotp(sharedKey, usedAt)).code)
  await expect(otherPage).toHaveURL(`${ADMIN_URL}/tk`)

  await page.goto(`${ADMIN_URL}/tk/security`)

  const sessions = page.getByTestId('session')

  await expect(sessions).toHaveCount(2)
  await expect(sessions.filter({ hasText: 'Şu enjam' })).toHaveCount(1)

  await sessions.filter({ hasNotText: 'Şu enjam' }).getByRole('button').click()
  await page.getByRole('dialog').getByRole('button', { name: 'Sessiýany ýap' }).click()
  await expect(sessions).toHaveCount(1)

  // The other browser's token dies with its session, and it ends up signed out.
  await otherPage.goto(`${ADMIN_URL}/tk/security`)
  await expect(otherPage).toHaveURL(/\/login/)

  await other.close()
})
