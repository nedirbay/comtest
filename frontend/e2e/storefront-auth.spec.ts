import type { Page } from '@playwright/test'
import { expect, test } from './support/fixtures'
import {
  createConfirmedUser,
  expectNoTokensInBrowserStorage,
  linkFromMail,
  PASSWORD,
  STOREFRONT_URL,
  uniqueEmail,
} from './support/stack'

async function fillLogin(page: Page, login: string, password: string): Promise<void> {
  await page.getByLabel('E-poçta ýa-da telefon').fill(login)
  await page.locator('input[type="password"]').fill(password)
  await page.getByRole('button', { name: 'Girmek', exact: true }).click()
}

test.describe('server rendering', () => {
  test('sends the store name in the HTML, and no secrets in the payload', async ({ request }) => {
    const html = await (await request.get(STOREFRONT_URL)).text()

    expect(html).toContain('Demo')
    expect(html).toMatch(/<html[^>]*lang="tk/)
    expect(html).not.toMatch(/eyJ[\w-]+\.[\w-]+\.[\w-]+/)
    expect(html).not.toContain('accessToken')
    expect(html).not.toContain('apiInternalUrl')
  })

  test('renders the language from the address', async ({ request }) => {
    const html = await (await request.get(`${STOREFRONT_URL}/ru/login`)).text()

    expect(html).toMatch(/<html[^>]*lang="ru/)
    expect(html).toContain('Вход в систему')
  })

  test('declares the cascade layers before any other styles', async ({ request }) => {
    const html = await (await request.get(STOREFRONT_URL)).text()
    const layers = html.indexOf('@layer theme')
    const stylesheet = html.search(/<link[^>]+stylesheet/)

    expect(layers).toBeGreaterThan(-1)
    expect(stylesheet === -1 || layers < stylesheet).toBe(true)
  })

  test('keeps private pages out of server rendering and search', async ({ request }) => {
    const response = await request.get(`${STOREFRONT_URL}/account`)

    expect(response.headers()['x-robots-tag']).toBe('noindex')
    expect(response.headers()['cache-control']).toBe('no-store')
    expect(await response.text()).not.toContain('profile-name')
  })
})

test.describe('opening the store directly', () => {
  test('the home page hydrates without a mismatch and offers sign-in', async ({
    page,
  }, testInfo) => {
    await page.goto(STOREFRONT_URL)

    await expect(page.getByTestId('store-hero')).toContainText('Demo Dükan')
    // A phone reaches the account through the bottom menu instead of the header.
    await expect(
      testInfo.project.name === 'phone'
        ? page.getByTestId('bottom-nav').getByRole('link', { name: 'Hasabym' })
        : page.getByRole('link', { name: 'Girmek' }),
    ).toBeVisible()
    await expect(page.locator('footer')).toContainText('Demo Dükan')
  })

  test('a signed-in customer sees their name in the header after a reload', async ({ page }) => {
    const email = await createConfirmedUser('Merdan Ataýew')

    await page.goto(`${STOREFRONT_URL}/login`)
    await fillLogin(page, email, PASSWORD)
    await expect(page.getByTestId('profile-name')).toHaveValue('Merdan Ataýew')

    await page.goto(STOREFRONT_URL)
    await expect(page.getByTestId('account-link')).toContainText('Merdan Ataýew')
  })

  test.describe('in a Russian browser', () => {
    test.use({ locale: 'ru-RU' })

    test('the root address leads to the Russian store', async ({ page }) => {
      await page.goto(STOREFRONT_URL)

      await expect(page).toHaveURL(`${STOREFRONT_URL}/ru`)
      await expect(page.getByTestId('store-hero')).toContainText('Добро пожаловать')
    })
  })
})

test('a new customer registers, confirms their email and stays signed in', async ({ page }) => {
  const email = uniqueEmail('new')

  await page.goto(`${STOREFRONT_URL}/register`)
  await page.getByLabel('Adyňyz we familiýaňyz').fill('Aýna Täze')
  await page.getByLabel('E-poçta', { exact: true }).fill(email)
  await page.locator('input[type="password"]').fill(PASSWORD)
  await page.getByRole('button', { name: 'Hasaba alynmak' }).click()
  await expect(page.getByText('E-poçtaňyzy barlaň')).toBeVisible()

  await page.goto(await linkFromMail(email, 'confirm-email'))
  await expect(page.getByText('E-poçta tassyklandy')).toBeVisible()
  // The token must not stay in the address bar or the browser history.
  expect(page.url()).not.toContain('token')

  await page.getByRole('link', { name: 'Girmek' }).last().click()
  await fillLogin(page, email, PASSWORD)
  await expect(page.getByTestId('profile-name')).toHaveValue('Aýna Täze')
  await expectNoTokensInBrowserStorage(page)

  // The token lives in memory, so a reload has to restore the session from the cookie.
  await page.reload()
  await expect(page.getByTestId('profile-name')).toHaveValue('Aýna Täze')
  await expectNoTokensInBrowserStorage(page)

  await page.getByTestId('logout').click()
  await expect(page).toHaveURL(`${STOREFRONT_URL}/`)
  await page.goto(`${STOREFRONT_URL}/account`)
  await expect(page).toHaveURL(/\/login\?returnUrl=/)
})

test('a wrong password gets the same message as an unknown account', async ({ page }) => {
  const email = await createConfirmedUser()

  await page.goto(`${STOREFRONT_URL}/login`)
  await fillLogin(page, email, 'not-the-right-password')

  const wrongPassword = page.getByRole('alert')

  await expect(wrongPassword).toBeVisible()

  const message = await wrongPassword.innerText()

  await fillLogin(page, uniqueEmail('nobody'), 'not-the-right-password')
  await expect(page.getByRole('alert')).toHaveText(message)
})

test('sign-in returns to the page the customer came from', async ({ page }) => {
  const email = await createConfirmedUser()

  await page.goto(`${STOREFRONT_URL}/account/security`)
  await expect(page).toHaveURL(/\/login\?returnUrl=/)
  await fillLogin(page, email, PASSWORD)
  await expect(page).toHaveURL(`${STOREFRONT_URL}/account/security`)
})

test('sign-in ignores a return address on another site', async ({ page }) => {
  const email = await createConfirmedUser()

  await page.goto(`${STOREFRONT_URL}/login?returnUrl=https://evil.example/phish`)
  await fillLogin(page, email, PASSWORD)
  await expect(page).toHaveURL(`${STOREFRONT_URL}/account`)
})

test('a forgotten password can be reset, and the old one stops working', async ({ page }) => {
  const email = await createConfirmedUser()
  const newPassword = 'another-long-passphrase-for-e2e'

  await page.goto(`${STOREFRONT_URL}/forgot-password`)
  await page.getByLabel('E-poçta', { exact: true }).fill(email)
  await page.getByRole('button', { name: 'Salgyny ibermek' }).click()
  await expect(page.getByText('Haty barlaň')).toBeVisible()

  await page.goto(await linkFromMail(email, 'reset-password'))
  await expect(page.getByLabel('Täze açar sözi', { exact: true })).toBeVisible()
  expect(page.url()).not.toContain('token')

  await page.getByLabel('Täze açar sözi', { exact: true }).fill(newPassword)
  await page.getByLabel('Täze açar sözüni gaýtalaň').fill(newPassword)
  await page.getByRole('button', { name: 'Açar sözüni täzelemek' }).click()
  await expect(page.getByText('Açar sözi täzelendi')).toBeVisible()

  await page.goto(`${STOREFRONT_URL}/login`)
  await fillLogin(page, email, PASSWORD)
  await expect(page.getByRole('alert')).toBeVisible()

  await fillLogin(page, email, newPassword)
  await expect(page).toHaveURL(`${STOREFRONT_URL}/account`)
})

test('forms check the input before calling the server', async ({ page }) => {
  let posted = false

  page.on('request', (request) => {
    posted ||= request.method() === 'POST' && request.url().includes('/auth/register')
  })

  await page.goto(`${STOREFRONT_URL}/register`)
  await page.getByLabel('E-poçta', { exact: true }).fill('not-an-email')
  await page.locator('input[type="password"]').fill('short')
  await page.getByRole('button', { name: 'Hasaba alynmak' }).click()

  await expect(page.getByText('Dogry e-poçta salgysyny giriziň')).toBeVisible()
  await expect(page.getByText('Açar sözi azyndan 12 belgiden ybarat bolmaly')).toBeVisible()
  expect(posted).toBe(false)
})

test('the password fields work with password managers', async ({ page }) => {
  await page.goto(`${STOREFRONT_URL}/login`)
  await expect(page.locator('input[autocomplete="username"]')).toBeVisible()
  await expect(page.locator('input[autocomplete="current-password"]')).toBeVisible()

  await page.goto(`${STOREFRONT_URL}/register`)
  await expect(page.locator('input[autocomplete="new-password"]')).toBeVisible()
})

test('the language switcher changes the address and the text', async ({ page }) => {
  await page.goto(`${STOREFRONT_URL}/login`, { waitUntil: 'networkidle' })
  await page.getByRole('button', { name: 'Dil' }).click()
  await page.getByRole('menuitem', { name: 'Русский' }).click()

  await expect(page).toHaveURL(`${STOREFRONT_URL}/ru/login`)
  await expect(page.getByRole('heading', { name: 'Вход в систему' })).toBeVisible()
})

test('the dark theme survives a reload without a flash', async ({ page, request }) => {
  await page.goto(`${STOREFRONT_URL}/login`, { waitUntil: 'networkidle' })
  await page.getByRole('button', { name: 'Gara tema' }).click()
  await expect(page.locator('html')).toHaveClass(/dark/)

  await page.reload()
  await expect(page.locator('html')).toHaveClass(/dark/)

  const cookies = await page.context().cookies()
  const theme = cookies.find((cookie) => cookie.name === 'comtest-theme')

  // The class has to be in the HTML the server sends, or dark mode would start white.
  const html = await (
    await request.get(`${STOREFRONT_URL}/login`, {
      headers: { Cookie: `comtest-theme=${theme?.value}` },
    })
  ).text()

  expect(html).toMatch(/<html[^>]*class="[^"]*dark/)
})

test('an unknown page shows a friendly 404', async ({ page }) => {
  const response = await page.goto(`${STOREFRONT_URL}/no/such/page`)

  expect(response?.status()).toBe(404)
  await expect(page.getByRole('heading', { name: 'Sahypa tapylmady' })).toBeVisible()
})
