import { readFileSync, writeFileSync } from 'node:fs'
import { randomBytes } from 'node:crypto'
import { deflateSync } from 'node:zlib'
import { expect, request, type APIRequestContext } from '@playwright/test'
import { API_URL, createStoreOwner, nextTotp, PASSWORD, totp } from './stack'

const STATE_FILE = '.e2e/demo-owner.json'

interface OwnerState {
  accessToken: string
  warehouseId: string
  categoryId: string
}

function readState(): OwnerState {
  return JSON.parse(readFileSync(STATE_FILE, 'utf8')) as OwnerState
}

/** The password `seed` printed for the demo store's owner. It only exists inside the .e2e folder. */
function seededOwner(): { email: string; password: string } {
  const line = readFileSync('.e2e/seed.log', 'utf8')
    .split('\n')
    .find((item) => /^\s*StoreOwner\s/.test(item))

  if (!line) {
    throw new Error('No StoreOwner in .e2e/seed.log. Run e2e/stack.sh up first.')
  }

  const [, email, password] = line.trim().split(/\s+/)

  return { email: email!, password: password! }
}

async function json<T>(response: Awaited<ReturnType<APIRequestContext['get']>>): Promise<T> {
  expect(response.ok(), `${response.url()}: ${await response.text()}`).toBe(true)

  return (await response.json()) as T
}

/** Signs the demo owner in through 2FA once per run, as the catalog pages need products to show. */
export async function prepareDemoOwner(): Promise<void> {
  const context = await request.newContext({ baseURL: API_URL })
  const { email, password } = seededOwner()
  const login = await json<{
    accessToken: string | null
    challengeToken: string | null
    twoFactorRequired: boolean
  }>(await context.post('/api/v1/auth/login', { data: { login: email, password } }))
  let keyFile: { sharedKey?: string } = {}

  try {
    keyFile = JSON.parse(readFileSync('.e2e/demo-owner-key.json', 'utf8')) as typeof keyFile
  } catch {
    // First run on this stack.
  }

  let accessToken = login.accessToken

  if (login.twoFactorRequired) {
    const { code } = await nextTotp(keyFile.sharedKey!, Date.now())
    const second = await json<{ accessToken: string }>(
      await context.post('/api/v1/auth/login/two-factor', {
        data: { challengeToken: login.challengeToken, code },
      }),
    )

    accessToken = second.accessToken
  } else {
    const headers = { Authorization: `Bearer ${accessToken}` }
    const setup = await json<{ sharedKey: string }>(
      await context.post('/api/v1/account/two-factor/setup', { headers }),
    )

    await json(
      await context.post('/api/v1/account/two-factor/enable', {
        headers,
        data: { code: totp(setup.sharedKey) },
      }),
    )
    writeFileSync('.e2e/demo-owner-key.json', JSON.stringify({ sharedKey: setup.sharedKey }))

    // The second factor is part of the session now, and the next token carries that.
    const { code } = await nextTotp(setup.sharedKey, Date.now())
    const relogin = await json<{ challengeToken: string }>(
      await context.post('/api/v1/auth/login', { data: { login: email, password } }),
    )
    const second = await json<{ accessToken: string }>(
      await context.post('/api/v1/auth/login/two-factor', {
        data: { challengeToken: relogin.challengeToken, code },
      }),
    )

    accessToken = second.accessToken
  }

  let headers = { Authorization: `Bearer ${accessToken}` }
  const [membership] = await json<{ tenantId: string }[]>(
    await context.get('/api/v1/account/tenants', { headers }),
  )
  const switched = await json<{ accessToken: string }>(
    await context.post('/api/v1/account/tenant', {
      headers,
      data: { tenantId: membership!.tenantId },
    }),
  )

  headers = { Authorization: `Bearer ${switched.accessToken}` }

  const [warehouse] = await json<{ id: string }[]>(
    await context.get('/api/v1/admin/inventory/warehouses', { headers }),
  )
  const categories = await json<{ id: string; slug: string }[]>(
    await context.get('/api/v1/admin/catalog/categories', { headers }),
  )

  // Test products get a category of their own, so that they do not push the demo data off page one.
  const testCategory =
    categories.find((item) => item.slug === 'e2e') ??
    (await json<{ id: string }>(
      await context.post('/api/v1/admin/catalog/categories', {
        headers,
        data: {
          slug: 'e2e',
          parentId: null,
          sortOrder: 99,
          translations: [{ language: 'tk', name: 'E2E' }],
        },
      }),
    ))

  writeFileSync(
    STATE_FILE,
    JSON.stringify({
      accessToken: switched.accessToken,
      warehouseId: warehouse!.id,
      categoryId: testCategory.id,
    } satisfies OwnerState),
  )
  await context.dispose()
}

async function ownerApi() {
  const state = readState()
  const context = await request.newContext({
    baseURL: API_URL,
    extraHTTPHeaders: { Authorization: `Bearer ${state.accessToken}` },
  })

  return { context, state }
}

export async function removeTestProducts(): Promise<void> {
  const { context } = await ownerApi()
  const slugs = new Map<string, string>()

  for (let page = 1; ; page += 1) {
    const result = await json<{ items: { id: string; slug: string }[]; hasNext: boolean }>(
      await context.get(`/api/v1/admin/catalog/products?page=${page}&pageSize=100`),
    )

    for (const item of result.items) {
      if (item.slug.startsWith('e2e-')) {
        slugs.set(item.id, item.slug)
      }
    }

    if (!result.hasNext) {
      break
    }
  }

  for (const id of slugs.keys()) {
    const response = await context.delete(`/api/v1/admin/catalog/products/${id}`)

    expect(response.ok(), await response.text()).toBe(true)
  }

  await context.dispose()
}

export interface VariantSpec {
  price: number
  stock?: number
  compareAt?: number
  options?: Record<string, string>
}

export interface ProductSpec {
  name?: string
  variants?: VariantSpec[]
  description?: string
  image?: boolean
}

export interface CreatedProduct {
  id: string
  slug: string
  name: string
  version: number
  variants: { id: string; sku: string; price: number; options: Record<string, string> }[]
}

function crc32(data: Buffer): number {
  let crc = ~0

  for (const byte of data) {
    crc ^= byte

    for (let bit = 0; bit < 8; bit += 1) {
      crc = crc & 1 ? (crc >>> 1) ^ 0xedb88320 : crc >>> 1
    }
  }

  return ~crc >>> 0
}

/** A real, decodable picture. The API re-encodes uploads, so a fake one would be rejected. */
function solidPng(size = 96): Buffer {
  const chunk = (type: string, data: Buffer) => {
    const length = Buffer.alloc(4)
    const body = Buffer.concat([Buffer.from(type), data])
    const crc = Buffer.alloc(4)

    length.writeUInt32BE(data.length)
    crc.writeUInt32BE(crc32(body))

    return Buffer.concat([length, body, crc])
  }
  const header = Buffer.alloc(13)

  header.writeUInt32BE(size, 0)
  header.writeUInt32BE(size, 4)
  header.writeUInt8(8, 8)
  header.writeUInt8(2, 9)

  const row = Buffer.concat([Buffer.from([0]), Buffer.alloc(size * 3, 0x2a)])
  const pixels = Buffer.concat(Array.from({ length: size }, () => row))

  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', header),
    chunk('IDAT', deflateSync(pixels)),
    chunk('IEND', Buffer.alloc(0)),
  ])
}

async function uploadImage(context: APIRequestContext): Promise<string> {
  const bytes = solidPng()
  const ticket = await json<{ id: string; uploadUrl: string }>(
    await context.post('/api/v1/admin/media/uploads', {
      data: { fileName: 'e2e.png', contentType: 'image/png', sizeBytes: bytes.length },
    }),
  )
  const uploaded = await context.put(ticket.uploadUrl, {
    data: bytes,
    headers: { 'Content-Type': 'image/png', Authorization: '' },
  })

  expect(uploaded.ok(), await uploaded.text()).toBe(true)
  await json(await context.post(`/api/v1/admin/media/${ticket.id}/complete`))

  await expect
    .poll(
      async () =>
        (await json<{ status: string }>(await context.get(`/api/v1/admin/media/${ticket.id}`)))
          .status,
      { timeout: 20_000 },
    )
    .toBe('Ready')

  return ticket.id
}

/** A published product in the demo store, with exactly the stock the test asks for. */
export async function createProduct(spec: ProductSpec = {}): Promise<CreatedProduct> {
  const { context, state } = await ownerApi()
  const unique = randomBytes(5).toString('hex')
  const name = spec.name ?? `E2E haryt ${unique}`
  const specs = spec.variants ?? [{ price: 100, stock: 50 }]
  const images = spec.image ? [{ mediaAssetId: await uploadImage(context), altText: name }] : []

  const saved = await json<CreatedProduct>(
    await context.post('/api/v1/admin/catalog/products', {
      data: {
        slug: `e2e-${unique}`,
        categoryId: state.categoryId,
        translations: [{ language: 'tk', name, description: spec.description ?? null }],
        variants: specs.map((variant, index) => ({
          id: null,
          sku: `E2E-${unique}-${index}`,
          price: variant.price,
          currency: 'TMT',
          compareAtAmount: variant.compareAt ?? null,
          options: variant.options ?? {},
        })),
        images,
      },
    }),
  )

  for (const [index, variant] of saved.variants.entries()) {
    const stock = specs[index]?.stock ?? 0

    if (stock > 0) {
      await json(
        await context.post('/api/v1/admin/inventory/stock/adjustments', {
          data: {
            warehouseId: state.warehouseId,
            variantId: variant.id,
            delta: stock,
            type: 'receipt',
            reason: 'e2e',
          },
        }),
      )
    }
  }

  const published = await json<CreatedProduct>(
    await context.put(`/api/v1/admin/catalog/products/${saved.id}/status`, {
      data: { status: 'published' },
    }),
  )

  await context.dispose()

  return { ...published, name }
}

export async function changePrice(product: CreatedProduct, price: number): Promise<void> {
  const { context } = await ownerApi()
  const current = await json<
    CreatedProduct & { translations: unknown[]; categoryId: string; images: unknown[] }
  >(await context.get(`/api/v1/admin/catalog/products/${product.id}`))

  await json(
    await context.put(`/api/v1/admin/catalog/products/${product.id}`, {
      data: {
        slug: current.slug,
        categoryId: current.categoryId,
        translations: current.translations,
        version: current.version,
        variants: current.variants.map((variant, index) => ({
          ...variant,
          price: index === 0 ? price : variant.price,
        })),
      },
    }),
  )
  await context.dispose()
}

export async function archiveProduct(product: CreatedProduct): Promise<void> {
  const { context } = await ownerApi()

  await json(
    await context.put(`/api/v1/admin/catalog/products/${product.id}/status`, {
      data: { status: 'archived' },
    }),
  )
  await context.dispose()
}

/**
 * Invites someone to a store of its own, because a plan allows only so many staff and pending
 * invitations and the demo store would run out. The owner first has to pass two-factor.
 */
export async function inviteStaffToNewStore(email: string): Promise<void> {
  const owner = await createStoreOwner('Çakylyk Dükany')
  const context = await request.newContext({ baseURL: API_URL })
  const login = await json<{ accessToken: string }>(
    await context.post('/api/v1/auth/login', { data: { login: owner.email, password: PASSWORD } }),
  )
  const firstHeaders = { Authorization: `Bearer ${login.accessToken}` }
  const setup = await json<{ sharedKey: string }>(
    await context.post('/api/v1/account/two-factor/setup', { headers: firstHeaders }),
  )
  const usedAt = Date.now()

  await json(
    await context.post('/api/v1/account/two-factor/enable', {
      headers: firstHeaders,
      data: { code: totp(setup.sharedKey, usedAt) },
    }),
  )

  const challenge = await json<{ challengeToken: string }>(
    await context.post('/api/v1/auth/login', { data: { login: owner.email, password: PASSWORD } }),
  )
  const { code } = await nextTotp(setup.sharedKey, usedAt)
  const session = await json<{ accessToken: string }>(
    await context.post('/api/v1/auth/login/two-factor', {
      data: { challengeToken: challenge.challengeToken, code },
    }),
  )
  const headers = { Authorization: `Bearer ${session.accessToken}` }
  const [membership] = await json<{ tenantId: string }[]>(
    await context.get('/api/v1/account/tenants', { headers }),
  )
  const switched = await json<{ accessToken: string }>(
    await context.post('/api/v1/account/tenant', {
      headers,
      data: { tenantId: membership!.tenantId },
    }),
  )

  await json(
    await context.post('/api/v1/admin/staff/invitations', {
      headers: { Authorization: `Bearer ${switched.accessToken}` },
      data: { email, role: 'Staff' },
    }),
  )
  await context.dispose()
}
