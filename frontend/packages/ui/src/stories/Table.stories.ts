import type { Meta, StoryObj } from '@storybook/vue3-vite'
import { ApiError } from '@comtest/api-client'
import type { Component } from 'vue'
import AppTable from '../components/AppTable.vue'
import { formatMoney } from '../format'

const columns = [
  { key: 'name', label: 'Haryt', minWidth: 200 },
  { key: 'sku', label: 'SKU', width: 120 },
  { key: 'price', label: 'Baha', width: 140, align: 'right' as const },
]

const rows = [
  { id: 1, name: 'Çaý, Altyn Asyr, 100 g', sku: 'TEA-100', price: 18.5 },
  { id: 2, name: 'Keçe halyça, 2×3 m', sku: 'RUG-23', price: 1250 },
  { id: 3, name: 'Gawun, Waharman', sku: 'MLN-1', price: 42 },
]

const meta: Meta = {
  title: 'Elements/AppTable',
  render: (args) => ({
    // A generic component does not fit the plain Component type that stories expect.
    components: { AppTable: AppTable as unknown as Component },
    setup: () => ({ args, columns, formatMoney }),
    template: `
      <AppTable :columns="columns" v-bind="args">
        <template #cell-price="{ row }">{{ formatMoney({ amount: row.price, currency: 'TMT' }, 'tk') }}</template>
      </AppTable>
    `,
  }),
}

export default meta

export const WithRows: StoryObj = { args: { rows } }
export const Paged: StoryObj = { args: { rows, total: 120, page: 2, pageSize: 20 } }
export const Loading: StoryObj = { args: { rows: [], loading: true } }
export const Empty: StoryObj = { args: { rows: [] } }
export const Failed: StoryObj = {
  args: {
    rows: [],
    error: new ApiError({ status: 500, code: 'common.unexpected', correlationId: 'a1b2c3d4' }),
  },
}
