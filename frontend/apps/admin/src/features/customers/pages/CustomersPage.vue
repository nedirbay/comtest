<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { formatPhone, PageHeader, useApiErrorMessage } from '@comtest/ui'
import { Search } from '@element-plus/icons-vue'
import { keepPreviousData, useQuery } from '@tanstack/vue-query'
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import DataTable, { type DataColumn } from '@/shared/components/DataTable.vue'
import { useFormat } from '@/shared/composables/useFormat'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { toastError } from '@/shared/toast'

type Customer = Schema<'CustomerSummaryDto'>

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const router = useRouter()
const format = useFormat()
const describeError = useApiErrorMessage()

const table = useTableQuery({ filters: { q: '' } })
const search = ref(table.filters.q)

watch(
  () => table.filters.q,
  (value) => {
    search.value = value
  },
)

const customers = useQuery({
  queryKey: [
    'admin',
    'customers',
    'list',
    computed(() => ({ page: table.page, pageSize: table.pageSize, q: table.filters.q })),
  ],
  placeholderData: keepPreviousData,
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/customers', {
        params: {
          query: {
            page: table.page,
            pageSize: table.pageSize,
            q: table.filters.q || undefined,
          },
        },
      }),
    ),
})

const rows = computed(() =>
  (customers.data.value?.items ?? []).map((customer) => ({
    ...customer,
    key: `${customer.userId ?? 'guest'}:${customer.phone}`,
  })),
)

const columns = computed<DataColumn[]>(() => [
  { key: 'name', label: t('admin.common.name'), minWidth: 180, required: true },
  { key: 'phone', label: t('admin.orders.phone'), minWidth: 150 },
  { key: 'email', label: t('admin.customers.email'), minWidth: 200, optional: true },
  { key: 'type', label: t('admin.customers.type'), minWidth: 130 },
  { key: 'orderCount', label: t('admin.customers.orderCount'), minWidth: 100, align: 'right' },
  { key: 'totalSpent', label: t('admin.customers.totalSpent'), minWidth: 140, align: 'right' },
  { key: 'lastOrderAt', label: t('admin.customers.lastOrderAt'), minWidth: 190 },
])

async function openProfile(customer: Customer): Promise<void> {
  if (customer.userId) {
    await router.push({ name: 'customer-profile', params: { userId: customer.userId } })

    return
  }

  try {
    const page = await unwrap(
      api.client.GET('/api/v1/admin/orders', {
        params: { query: { q: customer.phone, pageSize: 1, sort: '-placedAt' } },
      }),
    )
    const orderId = page.items[0]?.id

    if (orderId) {
      await router.push({ name: 'customer-guest', params: { orderId } })
    }
  } catch (error) {
    toastError(describeError(error))
  }
}
</script>

<template>
  <PageHeader :title="t('admin.nav.customers')" :description="t('admin.customers.intro')" />

  <DataTable
    id="customers"
    v-model:page="table.page"
    v-model:page-size="table.pageSize"
    row-key="key"
    :columns="columns"
    :rows="rows"
    :total="customers.data.value?.totalCount ?? 0"
    :loading="customers.isFetching.value"
    :error="customers.error.value"
    :has-filters="table.hasFilters"
    :empty-title="t(table.hasFilters ? 'admin.customers.noMatches' : 'admin.customers.empty')"
    @retry="customers.refetch()"
    @clear-filters="table.clearFilters()"
  >
    <template #filters>
      <el-input
        v-model="search"
        class="!w-full sm:!w-72"
        clearable
        :prefix-icon="Search"
        :placeholder="t('admin.customers.searchHint')"
        :aria-label="t('admin.customers.searchHint')"
        data-testid="filter-search"
        @keyup.enter="table.filters.q = search.trim()"
        @clear="table.filters.q = ''"
        @blur="table.filters.q = search.trim()"
      />
    </template>
    <template #cell-name="{ row }">
      <button
        type="button"
        class="text-left font-semibold text-primary"
        data-testid="customer-link"
        @click="openProfile(row)"
      >
        {{ row.name }}
      </button>
    </template>
    <template #cell-phone="{ row }">{{ formatPhone(row.phone) }}</template>
    <template #cell-type="{ row }">
      <el-tag :type="row.userId ? 'primary' : 'info'" disable-transitions>
        {{ t(row.userId ? 'admin.customers.registered' : 'admin.customers.guest') }}
      </el-tag>
    </template>
    <template #cell-totalSpent="{ row }">
      <span class="tabular-nums">{{ format.money(row.totalSpent, row.currency) }}</span>
    </template>
    <template #cell-lastOrderAt="{ row }">{{ format.dateTime(row.lastOrderAt) }}</template>
  </DataTable>
</template>
