import { expect, test } from './support/fixtures'
import { createStoreOwner } from './support/stack'

/** Runs like production: no X-Tenant header, so the host name alone picks the store. */
const SERVER = 'http://localhost:3002'

const storeHost = (slug: string) => `${slug}.comtest.local`

test('the server renders the store that the host name asks for', async ({ request }) => {
  const response = await request.get(SERVER, { headers: { Host: storeHost('demo') } })

  expect(await response.text()).toContain('Demo Dükan')
})

test('concurrent visitors of different stores never see each other', async ({ request }) => {
  test.slow()

  const first = { name: 'Birinji Dükan', ...(await createStoreOwner('Birinji Dükan')) }
  const second = { name: 'Ikinji Dükan', ...(await createStoreOwner('Ikinji Dükan')) }
  const visits = Array.from({ length: 30 }, (_, index) => (index % 2 ? first : second))

  const pages = await Promise.all(
    visits.map(async (store) => ({
      store,
      html: await (await request.get(SERVER, { headers: { Host: storeHost(store.slug) } })).text(),
    })),
  )

  for (const { store, html } of pages) {
    const other = store === first ? second : first

    expect(html).toContain(store.name)
    expect(html).not.toContain(other.name)
  }
})

test('a host name of no store gets a working page, not a crash', async ({ request }) => {
  const response = await request.get(SERVER, { headers: { Host: storeHost('no-such-store') } })

  expect(response.status()).toBe(200)
  expect(await response.text()).toContain('Dükan häzir elýeterli däl')
})

test('a forged host name cannot point the server at another address', async ({ request }) => {
  // With the host used to build the URL, this would make the server call its own metadata service.
  const response = await request.get(SERVER, { headers: { Host: '169.254.169.254' } })

  expect(response.status()).toBe(200)
  expect(await response.text()).toContain('Dükan häzir elýeterli däl')
})
