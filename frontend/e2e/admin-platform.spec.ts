import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { setUpTwoFactor, signIn } from './support/admin'
import { adminApi, expectNoSidewaysScroll, ok, watchToken } from './support/admin-data'
import { expect, test } from './support/fixtures'
import { ADMIN_URL, createStoreOwner, enterOtp, nextTotp } from './support/stack'

/**
 * There is no API to make a platform admin, so this uses the one the private stack seeded. Its
 * random password is in the stack's own log, and its two-factor key is kept next to it, so that a
 * later run can sign in again. All of that is removed by `e2e/stack.sh down`.
 */
const STATE = join(process.cwd(), '.e2e')
const SEED_LOG = join(STATE, 'seed.log')
const KEY_FILE = join(STATE, 'platform-admin-totp.json')
const ADMIN_EMAIL = 'admin@comtest.local'

function seededPassword(): string | null {
  if (!existsSync(SEED_LOG)) {
    return null
  }

  const line = readFileSync(SEED_LOG, 'utf8')
    .split('\n')
    .find((text) => text.includes(ADMIN_EMAIL) && text.includes('PlatformAdmin'))

  return line?.trim().split(/\s+/).pop() ?? null
}

test('the platform admin suspends and reactivates a firm, and sees no store pages', async ({
  page,
}, testInfo) => {
  test.setTimeout(180_000)
  // One seeded account, whose codes work once: two browsers at once would fight over them.
  test.skip(
    testInfo.project.name === 'phone',
    'Run once. The phone layout is checked by its own test below.',
  )

  const password = seededPassword()

  test.skip(password === null, 'The stack has no seed log with a platform admin.')

  const victim = await createStoreOwner('Togtadylýan Firma')
  const token = watchToken(page)

  await signIn(page, ADMIN_EMAIL, password!)

  if (existsSync(KEY_FILE)) {
    const { sharedKey, usedAt } = JSON.parse(readFileSync(KEY_FILE, 'utf8')) as {
      sharedKey: string
      usedAt: number
    }
    const { code, at } = await nextTotp(sharedKey, usedAt)

    await expect(page.getByText('Iki basgançakly tassyklama')).toBeVisible()
    await enterOtp(page, code)
    writeFileSync(KEY_FILE, JSON.stringify({ sharedKey, usedAt: at }))
  } else {
    const { sharedKey, usedAt } = await setUpTwoFactor(page)

    writeFileSync(KEY_FILE, JSON.stringify({ sharedKey, usedAt }))
  }

  await expect(page).toHaveURL(`${ADMIN_URL}/tk`)

  // The menu holds what a platform admin can do, and no store's pages.
  await expect(page.getByTestId('nav-tenants')).toBeVisible()
  await expect(page.getByTestId('nav-orders')).toHaveCount(0)
  await expect(page.getByTestId('nav-products')).toHaveCount(0)
  await expect(page.getByTestId('store-switcher')).toHaveCount(0)
  await expect(page.getByTestId('order-bell')).toHaveCount(0)

  await page.getByTestId('nav-tenants').click()
  await expect(page).toHaveURL(/\/platform\/tenants$/)
  await expect(page.getByTestId('tenant-row').first()).toBeVisible()
  await expectNoSidewaysScroll(page)

  if (process.env.ADMIN_SHOTS) {
    for (const [folder, size] of [
      ['desktop', { width: 1280, height: 720 }],
      ['phone', { width: 412, height: 839 }],
    ] as const) {
      mkdirSync(join(process.env.ADMIN_SHOTS, folder), { recursive: true })
      await page.setViewportSize(size)

      for (const theme of ['light', 'dark']) {
        await page.evaluate(
          (next) => window.localStorage.setItem('comtest.pref.theme', next),
          theme,
        )
        await page.reload()
        await expect(page.getByTestId('tenant-row').first()).toBeVisible()
        await expectNoSidewaysScroll(page)
        await page.screenshot({
          path: join(process.env.ADMIN_SHOTS, folder, `tenants-${theme}.png`),
        })
      }
    }

    await page.setViewportSize({ width: 1280, height: 720 })
    await page.evaluate(() => window.localStorage.setItem('comtest.pref.theme', 'light'))
  }

  // The list has no search, and the firm made just now can be on any page.
  const api = await adminApi(token.current())
  const all = await ok<{ items: { id: string; slug: string }[]; totalCount: number }>(
    await api.get('/api/v1/platform/tenants', { params: { pageSize: 100 } }),
  )
  let pageNumber = 1
  let firm = all.items.find((item) => item.slug === victim.slug)

  for (; !firm && pageNumber * 100 < all.totalCount; pageNumber += 1) {
    const next = await ok<typeof all>(
      await api.get('/api/v1/platform/tenants', {
        params: { pageSize: 100, page: pageNumber + 1 },
      }),
    )

    firm = next.items.find((item) => item.slug === victim.slug)
  }

  expect(firm, 'the new firm is listed').toBeTruthy()

  await page.goto(`${ADMIN_URL}/tk/platform/tenants?pageSize=100&page=${pageNumber}`)

  const row = page.getByRole('row').filter({ hasText: victim.slug })

  await expect(row).toContainText('Işjeň')

  // Saying no changes nothing.
  await row.getByTestId('tenant-suspend').click()

  const dialog = page.getByRole('dialog')

  await expect(dialog).toContainText('Togtadylýan Firma')
  await dialog.getByRole('button', { name: 'Ýatyr' }).click()
  await expect(row).toContainText('Işjeň')

  await row.getByTestId('tenant-suspend').click()
  await dialog.getByRole('button', { name: 'Togtat' }).click()
  await expect(row).toContainText('Togtadylan')

  await page.goto(`${ADMIN_URL}/tk/platform/tenants?status=suspended&pageSize=100`)
  await expect(page.getByRole('row').filter({ hasText: victim.slug })).toContainText('Togtadylan')
  await page
    .getByRole('row')
    .filter({ hasText: victim.slug })
    .getByTestId('tenant-activate')
    .click()
  await dialog.getByRole('button', { name: 'Işjeňleşdir' }).click()
  await expect(page.getByRole('row').filter({ hasText: victim.slug })).toHaveCount(0)

  await api.dispose()
})
