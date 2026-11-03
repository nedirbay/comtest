<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { ORDER_STATUSES, PAYMENT_STATES, PageHeader, StatusTag } from '@comtest/ui'
import { Search } from '@element-plus/icons-vue'
import { keepPreviousData, useQuery } from '@tanstack/vue-query'
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'
import { dayBoundary } from '@/features/dashboard/days'
import DataTable, { type DataColumn } from '@/shared/components/DataTable.vue'
import { useCustomerPrivacy } from '@/shared/composables/useCustomerPrivacy'
import { useFormat } from '@/shared/composables/useFormat'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { ORDERS_KEY } from '../queries'

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const format = useFormat()
const privacy = useCustomerPrivacy()

const table = useTableQuery({
  filters: { status: '', paymentState: '', from: '', to: '', q: '' },
  defaultSort: '-placedAt',
})

const search = ref(table.filters.q)

watch(
  () => table.filters.q,
  (value) => {
    search.value = value
  },
)

const orders = useQuery({
  queryKey: [
    ...ORDERS_KEY,
    'list',
    computed(() => ({
      page: table.page,
      pageSize: table.pageSize,
      sort: table.sort,
      ...table.filters,
    })),
  ],
  placeholderData: keepPreviousData,
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/orders', {
        params: {
          query: {
            page: table.page,
            pageSize: table.pageSize,
            sort: table.sort || undefined,
            status: (table.filters.status || undefined) as Schema<'OrderStatus'> | undefined,
            paymentState: (table.filters.paymentState || undefined) as
              Schema<'OrderPaymentState'> | undefined,
            from: table.filters.from ? dayBoundary(table.filters.from, 'start') : undefined,
            to: table.filters.to ? dayBoundary(table.filters.to, 'end') : undefined,
            q: table.filters.q || undefined,
          },
        },
      }),
    ),
})

const columns = computed<DataColumn[]>(() => [
  { key: 'number', label: t('admin.orders.number'), minWidth: 170, sortable: true, required: true },
  { key: 'placedAt', label: t('admin.orders.placedAt'), minWidth: 190, sortable: true },
  { key: 'contactName', label: t('admin.orders.customer'), minWidth: 160 },
  { key: 'contactPhone', label: t('admin.orders.phone'), minWidth: 150, optional: true },
  { key: 'status', label: t('admin.orders.status'), minWidth: 130, sortable: true },
  { key: 'paymentState', label: t('admin.orders.payment'), minWidth: 150 },
  { key: 'paymentMethod', label: t('admin.orders.paymentMethod'), minWidth: 170, optional: true },
  {
    key: 'itemCount',
    label: t('admin.orders.itemCount'),
    minWidth: 90,
    align: 'right',
    optional: true,
  },
  { key: 'total', label: t('admin.orders.total'), minWidth: 130, align: 'right', sortable: true },
])
</script>

<template>
  <PageHeader :title="t('admin.nav.orders')" :description="t('admin.orders.intro')" />

  <DataTable
    id="orders"
    v-model:page="table.page"
    v-model:page-size="table.pageSize"
    v-model:sort="table.sort"
    :columns="columns"
    :rows="orders.data.value?.items ?? []"
    :total="orders.data.value?.totalCount ?? 0"
    :loading="orders.isFetching.value"
    :error="orders.error.value"
    :has-filters="table.hasFilters"
    :empty-title="t(table.hasFilters ? 'admin.orders.noMatches' : 'admin.orders.empty')"
    @retry="orders.refetch()"
    @clear-filters="table.clearFilters()"
  >
    <template #filters>
      <el-input
        v-model="search"
        class="!w-full sm:!w-56"
        clearable
        :prefix-icon="Search"
        :placeholder="t('admin.orders.searchHint')"
        :aria-label="t('admin.orders.searchHint')"
        data-testid="filter-search"
        @keyup.enter="table.filters.q = search.trim()"
        @clear="table.filters.q = ''"
        @blur="table.filters.q = search.trim()"
      />
      <el-select
        v-model="table.filters.status"
        class="!w-full sm:!w-40"
        clearable
        value-on-clear=""
        :empty-values="['', null, undefined]"
        :placeholder="t('admin.orders.allStatuses')"
        :aria-label="t('admin.orders.status')"
        data-testid="filter-status"
      >
        <el-option
          v-for="status in ORDER_STATUSES"
          :key="status"
          :value="status"
          :label="t(`enums.orderStatus.${status}`)"
        />
      </el-select>
      <el-select
        v-model="table.filters.paymentState"
        class="!w-full sm:!w-44"
        clearable
        value-on-clear=""
        :empty-values="['', null, undefined]"
        :placeholder="t('admin.orders.allPayments')"
        :aria-label="t('admin.orders.payment')"
        data-testid="filter-payment"
      >
        <el-option
          v-for="state in PAYMENT_STATES"
          :key="state"
          :value="state"
          :label="t(`enums.paymentState.${state}`)"
        />
      </el-select>
      <el-date-picker
        v-model="table.filters.from"
        class="!w-full sm:!w-40"
        type="date"
        value-format="YYYY-MM-DD"
        value-on-clear=""
        :placeholder="t('admin.orders.from')"
        :aria-label="t('admin.orders.from')"
      />
      <el-date-picker
        v-model="table.filters.to"
        class="!w-full sm:!w-40"
        type="date"
        value-format="YYYY-MM-DD"
        value-on-clear=""
        :placeholder="t('admin.orders.to')"
        :aria-label="t('admin.orders.to')"
      />
    </template>
    <template #cell-number="{ row }">
      <RouterLink
        :to="{ name: 'order-detail', params: { id: row.id } }"
        class="font-semibold text-primary"
        data-testid="order-link"
      >
        {{ row.number }}
      </RouterLink>
    </template>
    <template #cell-placedAt="{ row }">{{ format.dateTime(row.placedAt) }}</template>
    <template #cell-contactName="{ row }">{{ privacy.name(row.contactName) }}</template>
    <template #cell-contactPhone="{ row }">{{ privacy.phone(row.contactPhone) }}</template>
    <template #cell-status="{ row }">
      <StatusTag kind="orderStatus" :value="row.status" />
    </template>
    <template #cell-paymentState="{ row }">
      <StatusTag kind="paymentState" :value="row.paymentState" />
    </template>
    <template #cell-paymentMethod="{ row }">
      <StatusTag kind="paymentMethod" :value="row.paymentMethod" />
    </template>
    <template #cell-total="{ row }">
      <span class="font-semibold tabular-nums">{{ format.money(row.total, row.currency) }}</span>
    </template>
  </DataTable>
</template>
