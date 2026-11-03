import { describe, expect, it } from 'vitest'
import { createApp, h, nextTick } from 'vue'
import { createMemoryHistory, createRouter, RouterView } from 'vue-router'
import { useTableQuery } from '../src/shared/composables/useTableQuery'

async function setup(start: string) {
  let table!: ReturnType<typeof useTableQuery<{ status: string; q: string }>>

  const Page = {
    setup() {
      table = useTableQuery({ filters: { status: '', q: '' }, defaultSort: '-placedAt' })

      return () => h('div')
    },
  }

  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/list', name: 'list', component: Page },
      { path: '/other', name: 'other', component: { render: () => h('div') } },
    ],
  })

  await router.push(start)

  const app = createApp({ render: () => h(RouterView) })

  app.use(router)
  app.mount(document.createElement('div'))
  await router.isReady()
  await nextTick()

  return { router, table }
}

const settled = async (router: { isReady: () => Promise<void> }) => {
  await router.isReady()
  await nextTick()
  await new Promise((resolve) => setTimeout(resolve))
}

describe('a list that keeps its state in the address', () => {
  it('reads what the address says', async () => {
    const { table } = await setup('/list?status=paid&page=3&pageSize=50&sort=total')

    expect(table.filters.status).toBe('paid')
    expect(table.page).toBe(3)
    expect(table.pageSize).toBe(50)
    expect(table.sort).toBe('total')
    expect(table.hasFilters).toBe(true)
  })

  it('falls back on nonsense rather than sending it to the API', async () => {
    const { table } = await setup('/list?page=-4&pageSize=99999')

    expect(table.page).toBe(1)
    expect(table.pageSize).toBe(20)
    expect(table.sort).toBe('-placedAt')
    expect(table.hasFilters).toBe(false)
  })

  it('writes a filter into the address, and goes back to the first page', async () => {
    const { router, table } = await setup('/list?page=4')

    table.filters.status = 'pending'
    await settled(router)

    expect(router.currentRoute.value.query).toEqual({ status: 'pending' })
    expect(table.page).toBe(1)
  })

  it('leaves defaults out of the address', async () => {
    const { router, table } = await setup('/list?sort=total&pageSize=50')

    table.sort = '-placedAt'
    await settled(router)
    table.pageSize = 20
    await settled(router)

    expect(router.currentRoute.value.query).toEqual({})
  })

  it('clears every filter at once, and keeps the sort', async () => {
    const { router, table } = await setup('/list?status=paid&q=abc&page=2&sort=total')

    table.clearFilters()
    await settled(router)

    expect(router.currentRoute.value.query).toEqual({ sort: 'total' })
    expect(table.hasFilters).toBe(false)
  })

  it('keeps its values while the router moves to another page', async () => {
    const { router, table } = await setup('/list?status=paid')

    await router.push('/other')
    await nextTick()

    expect(table.filters.status).toBe('paid')
  })
})
