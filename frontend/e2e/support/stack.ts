import { createHmac, randomBytes } from 'node:crypto'
import { expect, request, type APIRequestContext, type Page } from '@playwright/test'

export const API_URL = 'http://localhost:5055'
export const STOREFRONT_URL = 'http://localhost:3001'
export const ADMIN_URL = 'http://localhost:5174'
const MAILBOX_URL = 'http://localhost:58025'

export const PASSWORD = 'a-long-test-passphrase-for-e2e'

export function uniqueEmail(prefix: string): string {
  return `${prefix}-${randomBytes(6).toString('hex')}@e2e.comtest.local`
}

/** Waits for the newest mail to an address and returns the first link to the given page. */
export async function linkFromMail(email: string, page: string): Promise<string> {
  const context = await request.newContext({ baseURL: MAILBOX_URL })
  const pattern = new RegExp(`https?://\\S*/${page}#\\S+`)
  let link: string | undefined

  await expect
    .poll(
      async () => {
        const search = await context.get('/api/v1/search', {
          params: { query: `to:"${email}"` },
        })
        const { messages } = (await search.json()) as { messages: { ID: string }[] }

        for (const message of messages) {
          const detail = await context.get(`/api/v1/message/${message.ID}`)
          const { Text } = (await detail.json()) as { Text: string }

          link = pattern.exec(Text)?.[0]

          if (link) {
            return true
          }
        }

        return false
      },
      { timeout: 20_000, message: `no mail with a /${page} link reached ${email}` },
    )
    .toBe(true)

  await context.dispose()

  return link!
}

/** RFC 6238, as an authenticator app computes it. */
export function totp(sharedKey: string, at = Date.now()): string {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'
  const bits = sharedKey
    .replace(/[\s=-]/g, '')
    .toUpperCase()
    .split('')
    .map((char) => alphabet.indexOf(char).toString(2).padStart(5, '0'))
    .join('')
  const key = Buffer.from(bits.match(/.{8}/g)!.map((byte) => parseInt(byte, 2)))
  const counter = Buffer.alloc(8)

  counter.writeBigUInt64BE(BigInt(Math.floor(at / 30_000)))

  const hash = createHmac('sha1', key).update(counter).digest()
  const offset = hash[hash.length - 1]! & 0xf
  const code = (hash.readUInt32BE(offset) & 0x7fffffff) % 1_000_000

  return code.toString().padStart(6, '0')
}

function api(): Promise<APIRequestContext> {
  return request.newContext({ baseURL: API_URL })
}

/** An account that has already confirmed its email, ready to sign in. */
export async function createConfirmedUser(fullName = 'E2E Müşderi'): Promise<string> {
  const email = uniqueEmail('user')
  const context = await api()
  const registered = await context.post('/api/v1/auth/register', {
    data: { email, password: PASSWORD, fullName, locale: 'tk' },
  })

  expect(registered.status()).toBe(202)

  const link = await linkFromMail(email, 'confirm-email')
  const params = new URLSearchParams(link.split('#')[1])
  const confirmed = await context.post('/api/v1/auth/confirm-email', {
    data: { userId: params.get('userId'), token: params.get('token') },
  })

  expect(confirmed.ok()).toBe(true)
  await context.dispose()

  return email
}

/** A new firm with its owner, whose first sign-in has to set up two-factor. */
export async function createStoreOwner(
  storeName = 'E2E Dükany',
): Promise<{ email: string; slug: string }> {
  const email = await createConfirmedUser('E2E Eýe')
  const context = await api()
  const login = await context.post('/api/v1/auth/login', {
    data: { login: email, password: PASSWORD },
  })
  const { accessToken } = (await login.json()) as { accessToken: string }
  const slug = `e2e-${randomBytes(5).toString('hex')}`
  const created = await context.post('/api/v1/tenants', {
    headers: { Authorization: `Bearer ${accessToken}` },
    data: { name: storeName, slug, defaultCurrency: 'TMT', defaultLanguage: 'tk' },
  })

  expect(created.ok(), await created.text()).toBe(true)

  // Creating a firm changes the user's security stamp, which retires the token used until now.
  const { accessToken: ownerToken } = (await created.json()) as { accessToken: string }

  // This setup should not show up as one more device in the tests that count sessions.
  const { sid } = JSON.parse(Buffer.from(ownerToken.split('.')[1]!, 'base64url').toString()) as {
    sid: string
  }
  const closed = await context.delete(`/api/v1/account/sessions/${sid}`, {
    headers: { Authorization: `Bearer ${ownerToken}` },
  })

  expect(closed.ok(), await closed.text()).toBe(true)
  await context.dispose()

  return { email, slug }
}

/** Types a code into the six boxes. A filled last box may submit the form by itself. */
export async function enterOtp(page: Page, code: string): Promise<void> {
  await page.locator('.el-input-otp input').first().click()
  await page.keyboard.type(code)
}

/** A code from a later time window, as the server accepts each one only once. */
export async function nextTotp(
  sharedKey: string,
  usedAt: number,
): Promise<{ code: string; at: number }> {
  const nextWindow = (Math.floor(usedAt / 30_000) + 1) * 30_000

  await new Promise((resolve) => setTimeout(resolve, Math.max(0, nextWindow - Date.now() + 200)))

  const at = Date.now()

  return { code: totp(sharedKey, at), at }
}

/** What a script on the page could read. A token must never be among it. */
export function readableBrowserStorage(page: Page): Promise<string> {
  return page.evaluate(() =>
    JSON.stringify({
      local: { ...window.localStorage },
      session: { ...window.sessionStorage },
      cookie: document.cookie,
    }),
  )
}

const JWT = /eyJ[\w-]+\.[\w-]+\.[\w-]+/

export async function expectNoTokensInBrowserStorage(page: Page): Promise<void> {
  expect(await readableBrowserStorage(page)).not.toMatch(JWT)
}
