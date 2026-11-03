<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { ErrorState, formatPhone, PageHeader, SkeletonBlock, StatusTag } from '@comtest/ui'
import { keepPreviousData, useQuery } from '@tanstack/vue-query'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink, useRoute } from 'vue-router'
import { usePageTitle } from '@/features/shell/useShellStore'
import DataTable, { type DataColumn } from '@/shared/components/DataTable.vue'
import { useFormat } from '@/shared/composables/useFormat'
import { useTableQuery } from '@/shared/composables/useTableQuery'

const SUMMARY_LOOKUP_SIZE = 100

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const route = useRoute()
const format = useFormat()

const userId = route.name === 'customer-profile' ? String(route.params.userId) : null
const guestOrderId = route.name === 'customer-guest' ? String(route.params.orderId) : null

const table = useTableQuery({ filters: {}, defaultSort: '-placedAt' })

// The API has no customer endpoint, so a profile is pieced together from orders and the customer
// list. Asking through an order keeps the phone number out of this page's address.
const phone = useQuery({
  queryKey: ['admin', 'customers', 'phone', userId ?? guestOrderId],
  queryFn: async (): Promise<string | null> => {
    if (guestOrderId) {
      const order = await unwrap(
        api.client.GET('/api/v1/admin/orders/{id}', { params: { path: { id: guestOrderId } } }),
      )

      return order.order.contact.phone
    }

    const page = await unwrap(
      api.client.GET('/api/v1/admin/orders', {
        params: { query: { customerUserId: userId as string, pageSize: 1, sort: '-placedAt' } },
      }),
    )

    return page.items[0]?.contactPhone ?? null
  },
})

const summary = useQuery({
  queryKey: ['admin', 'customers', 'summary', userId, phone.data],
  enabled: computed(() => Boolean(phone.data.value)),
  queryFn: async () => {
    const page = await unwrap(
      api.client.GET('/api/v1/admin/customers', {
        params: { query: { q: phone.data.value as string, pageSize: SUMMARY_LOOKUP_SIZE } },
      }),
    )

    return page.items.find((item) => item.userId === userId) ?? null
  },
})

usePageTitle(() => summary.data.value?.name)

const orders = useQuery({
  queryKey: [
    'admin',
    'orders',
    'customer',
    userId,
    phone.data,
    computed(() => ({ page: table.page, pageSize: table.pageSize, sort: table.sort })),
  ],
  enabled: computed(() => userId !== null || Boolean(phone.data.value)),
  placeholderData: keepPreviousData,
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/orders', {
        params: {
          query: {
            page: table.page,
            pageSize: table.pageSize,
            sort: table.sort || undefined,
            ...(userId ? { customerUserId: userId } : { q: phone.data.value as string }),
          },
        },
      }),
    ),
})

const columns = computed<DataColumn[]>(() => [
  { key: 'number', label: t('admin.orders.number'), minWidth: 170, sortable: true, required: true },
  { key: 'placedAt', label: t('admin.orders.placedAt'), minWidth: 190, sortable: true },
  { key: 'status', label: t('admin.orders.status'), minWidth: 130, sortable: true },
  { key: 'paymentState', label: t('admin.orders.payment'), minWidth: 150 },
  { key: 'total', label: t('admin.orders.total'), minWidth: 130, align: 'right', sortable: true },
])
</script>

<template>
  <ErrorState
    v-if="phone.isError.value"
    :error="phone.error.value"
    retryable
    @retry="phone.refetch()"
  />
  <SkeletonBlock v-else-if="phone.isPending.value || summary.isLoading.value" :lines="6" />
  <template v-else>
    <PageHeader :title="summary.data.value?.name ?? t('admin.customers.profile')">
      <template #actions>
        <el-tag :type="userId ? 'primary' : 'info'" disable-transitions>
          {{ t(userId ? 'admin.customers.registered' : 'admin.customers.guest') }}
        </el-tag>
      </template>
    </PageHeader>

    <section
      v-if="summary.data.value"
      class="mb-6 grid gap-4 rounded-card border border-line bg-surface p-4 sm:grid-cols-2 lg:grid-cols-5"
      data-testid="customer-summary"
    >
      <div>
        <p class="text-xs text-ink-muted">{{ t('admin.orders.phone') }}</p>
        <p class="font-semibold text-ink">{{ formatPhone(summary.data.value.phone) }}</p>
      </div>
      <div class="min-w-0">
        <p class="text-xs text-ink-muted">{{ t('admin.customers.email') }}</p>
        <p class="break-words font-semibold text-ink">{{ summary.data.value.email ?? '—' }}</p>
      </div>
      <div>
        <p class="text-xs text-ink-muted">{{ t('admin.customers.orderCount') }}</p>
        <p class="font-semibold tabular-nums text-ink" data-testid="customer-order-count">
          {{ summary.data.value.orderCount }}
        </p>
      </div>
      <div>
        <p class="text-xs text-ink-muted">{{ t('admin.customers.totalSpent') }}</p>
        <p class="font-semibold tabular-nums text-ink">
          {{ format.money(summary.data.value.totalSpent, summary.data.value.currency) }}
        </p>
      </div>
      <div>
        <p class="text-xs text-ink-muted">{{ t('admin.customers.lastOrderAt') }}</p>
        <p class="font-semibold text-ink">{{ format.dateTime(summary.data.value.lastOrderAt) }}</p>
      </div>
    </section>

    <h2 class="mb-3 font-bold text-ink">{{ t('admin.customers.ordersTitle') }}</h2>
    <DataTable
      id="customer-orders"
      v-model:page="table.page"
      v-model:page-size="table.pageSize"
      v-model:sort="table.sort"
      :columns="columns"
      :rows="orders.data.value?.items ?? []"
      :total="orders.data.value?.totalCount ?? 0"
      :loading="orders.isFetching.value"
      :error="orders.error.value"
      :empty-title="t('admin.customers.noOrders')"
      @retry="orders.refetch()"
    >
      <template #cell-number="{ row }">
        <RouterLink
          :to="{ name: 'order-detail', params: { id: row.id } }"
          class="font-semibold text-primary"
        >
          {{ row.number }}
        </RouterLink>
      </template>
      <template #cell-placedAt="{ row }">{{ format.dateTime(row.placedAt) }}</template>
      <template #cell-status="{ row }">
        <StatusTag kind="orderStatus" :value="row.status" />
      </template>
      <template #cell-paymentState="{ row }">
        <StatusTag kind="paymentState" :value="row.paymentState" />
      </template>
      <template #cell-total="{ row }">
        <span class="tabular-nums">{{ format.money(row.total, row.currency) }}</span>
      </template>
    </DataTable>
  </template>
</template>
