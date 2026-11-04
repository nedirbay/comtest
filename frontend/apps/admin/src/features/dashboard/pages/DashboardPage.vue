<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { useApi, useAuthStore } from '@comtest/auth'
import {
  AppTable,
  ErrorState,
  ORDER_STATUSES,
  PageHeader,
  SkeletonBlock,
  StatusTag,
  type TableColumn,
} from '@comtest/ui'
import { useQuery } from '@tanstack/vue-query'
import { computed, defineAsyncComponent } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink, type RouteLocationRaw } from 'vue-router'
import { Permission } from '@/app/permissions'
import { useCustomerPrivacy } from '@/shared/composables/useCustomerPrivacy'
import { useFormat } from '@/shared/composables/useFormat'
import { STORE_OFFSET_MINUTES, lastDays } from '../days'

const OrdersChart = defineAsyncComponent(() => import('../components/OrdersChart.vue'))

const CHART_DAYS = 14
const RECENT_ORDERS = 5
const TOP_PRODUCTS = 5

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const format = useFormat()
const privacy = useCustomerPrivacy()

const canOrders = computed(() => auth.hasActiveTenant && auth.can(Permission.OrdersRead))
const canProducts = computed(() => auth.hasActiveTenant && auth.can(Permission.CatalogRead))
const canStock = computed(() => auth.hasActiveTenant && auth.can(Permission.InventoryRead))
const canCustomers = computed(() => auth.hasActiveTenant && auth.can(Permission.CustomersRead))
const canReports = computed(() => auth.hasActiveTenant && auth.can(Permission.ReportsRead))
const canTenants = computed(() => auth.can(Permission.TenantsManage))

// Counts are a list's `totalCount`, and money comes from the report API: nothing is calculated here.
const statusCounts = useQuery({
  queryKey: ['admin', 'orders', 'dashboard', 'by-status'],
  enabled: canOrders,
  queryFn: () =>
    Promise.all(
      ORDER_STATUSES.map(async (status) => ({
        status,
        count: (
          await unwrap(
            api.client.GET('/api/v1/admin/orders', {
              params: { query: { status, pageSize: 1 } },
            }),
          )
        ).totalCount,
      })),
    ),
})

const unpaidCount = useQuery({
  queryKey: ['admin', 'orders', 'dashboard', 'unpaid'],
  enabled: canOrders,
  queryFn: async () =>
    (
      await unwrap(
        api.client.GET('/api/v1/admin/orders', {
          params: { query: { paymentState: 'unpaid', pageSize: 1 } },
        }),
      )
    ).totalCount,
})

const productCount = useQuery({
  queryKey: ['admin', 'catalog', 'products', 'dashboard'],
  enabled: canProducts,
  queryFn: async () =>
    (
      await unwrap(
        api.client.GET('/api/v1/admin/catalog/products', { params: { query: { pageSize: 1 } } }),
      )
    ).totalCount,
})

const lowStockCount = useQuery({
  queryKey: ['admin', 'inventory', 'stock', 'dashboard-low'],
  enabled: canStock,
  queryFn: async () =>
    (
      await unwrap(
        api.client.GET('/api/v1/admin/inventory/stock', {
          params: { query: { lowOnly: true, pageSize: 1 } },
        }),
      )
    ).totalCount,
})

const customerCount = useQuery({
  queryKey: ['admin', 'customers', 'dashboard'],
  enabled: canCustomers,
  queryFn: async () =>
    (
      await unwrap(
        api.client.GET('/api/v1/admin/customers', { params: { query: { pageSize: 1 } } }),
      )
    ).totalCount,
})

const tenantCount = useQuery({
  queryKey: ['platform', 'tenants', 'dashboard'],
  enabled: canTenants,
  queryFn: async () =>
    (
      await unwrap(
        api.client.GET('/api/v1/platform/tenants', { params: { query: { pageSize: 1 } } }),
      )
    ).totalCount,
})

const salesPeriod = lastDays(CHART_DAYS)

const sales = useQuery({
  queryKey: ['admin', 'reports', 'sales', 'dashboard'],
  enabled: canReports,
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/reports/sales', {
        params: {
          query: {
            from: salesPeriod[0]?.from,
            to: salesPeriod.at(-1)?.to,
            utcOffsetMinutes: STORE_OFFSET_MINUTES,
          },
        },
      }),
    ),
})

const topProducts = useQuery({
  queryKey: ['admin', 'reports', 'products', 'dashboard'],
  enabled: canReports,
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/reports/products', {
        params: {
          query: {
            from: salesPeriod[0]?.from,
            to: salesPeriod.at(-1)?.to,
            sort: 'units',
            limit: TOP_PRODUCTS,
          },
        },
      }),
    ),
})

const chartDays = computed(() =>
  (sales.data.value?.days ?? []).map((day) => ({
    label: `${day.date.slice(8, 10)}.${day.date.slice(5, 7)}`,
    count: day.orders,
  })),
)

const recentOrders = useQuery({
  queryKey: ['admin', 'orders', 'dashboard', 'recent'],
  enabled: canOrders,
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/orders', {
        params: { query: { pageSize: RECENT_ORDERS, sort: '-placedAt' } },
      }),
    ),
})

const recentColumns = computed<TableColumn[]>(() => [
  { key: 'number', label: t('admin.orders.number'), minWidth: 150 },
  { key: 'contactName', label: t('admin.orders.customer'), minWidth: 140 },
  { key: 'status', label: t('admin.orders.status'), minWidth: 130 },
  { key: 'total', label: t('admin.orders.total'), minWidth: 120, align: 'right' },
  { key: 'placedAt', label: t('admin.orders.placedAt'), minWidth: 180 },
])

interface Tile {
  key: string
  label: string
  value: number | string | undefined
  pending: boolean
  to: RouteLocationRaw
  attention?: boolean
}

const tiles = computed<Tile[]>(() => {
  const list: Tile[] = []

  if (canReports.value) {
    const report = sales.data.value

    list.push({
      key: 'revenue',
      label: t('admin.dashboard.revenue'),
      value: report && format.money(report.figures.revenue, report.currency),
      pending: sales.isPending.value,
      to: { name: 'reports' },
    })
    list.push({
      key: 'average',
      label: t('admin.dashboard.average'),
      value: report && format.money(report.figures.averageOrderValue, report.currency),
      pending: sales.isPending.value,
      to: { name: 'reports' },
    })
  }

  if (canOrders.value) {
    const pending = statusCounts.data.value?.find((item) => item.status === 'pending')?.count

    list.push({
      key: 'pending',
      label: t('admin.dashboard.pendingOrders'),
      value: pending,
      pending: statusCounts.isPending.value,
      to: { name: 'orders', query: { status: 'pending' } },
      attention: (pending ?? 0) > 0,
    })
    list.push({
      key: 'unpaid',
      label: t('admin.dashboard.unpaidOrders'),
      value: unpaidCount.data.value,
      pending: unpaidCount.isPending.value,
      to: { name: 'orders', query: { paymentState: 'unpaid' } },
    })
  }

  if (canProducts.value) {
    list.push({
      key: 'products',
      label: t('admin.dashboard.products'),
      value: productCount.data.value,
      pending: productCount.isPending.value,
      to: { name: 'products' },
    })
  }

  if (canStock.value) {
    list.push({
      key: 'low-stock',
      label: t('admin.dashboard.lowStock'),
      value: lowStockCount.data.value,
      pending: lowStockCount.isPending.value,
      to: { name: 'stock', query: { lowOnly: 'true' } },
      attention: (lowStockCount.data.value ?? 0) > 0,
    })
  }

  if (canCustomers.value) {
    list.push({
      key: 'customers',
      label: t('admin.dashboard.customers'),
      value: customerCount.data.value,
      pending: customerCount.isPending.value,
      to: { name: 'customers' },
    })
  }

  if (canTenants.value) {
    list.push({
      key: 'tenants',
      label: t('admin.nav.tenants'),
      value: tenantCount.data.value,
      pending: tenantCount.isPending.value,
      to: { name: 'tenants' },
    })
  }

  return list
})
</script>

<template>
  <PageHeader
    :title="t('admin.dashboard.greeting', { name: auth.profile?.fullName ?? '' })"
    :description="t('admin.dashboard.intro')"
  />

  <ul
    v-if="tiles.length"
    class="mb-6 grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-5"
    data-testid="dashboard-tiles"
  >
    <li v-for="tile in tiles" :key="tile.key">
      <RouterLink
        :to="tile.to"
        class="block h-full rounded-card border border-line bg-surface p-4 transition-colors hover:border-primary"
        :data-testid="`tile-${tile.key}`"
      >
        <span class="block min-h-10 text-sm font-medium leading-snug text-ink-muted">
          {{ tile.label }}
        </span>
        <span
          class="mt-1 block font-bold tracking-tight tabular-nums"
          :class="[
            tile.attention ? 'text-warning' : 'text-ink',
            typeof tile.value === 'string' ? 'text-xl leading-9' : 'text-3xl',
          ]"
          data-testid="tile-value"
        >
          {{ tile.pending ? '…' : (tile.value ?? '—') }}
        </span>
      </RouterLink>
    </li>
  </ul>

  <div v-if="canOrders" class="grid gap-6 xl:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
    <section
      v-if="canReports"
      class="min-w-0 rounded-card border border-line bg-surface p-4 sm:p-5"
    >
      <h2 class="font-bold text-ink">{{ t('admin.dashboard.chartTitle') }}</h2>
      <p class="mb-3 text-sm text-ink-muted">{{ t('admin.dashboard.chartHint') }}</p>
      <ErrorState
        v-if="sales.isError.value"
        :error="sales.error.value"
        retryable
        @retry="sales.refetch()"
      />
      <SkeletonBlock v-else-if="sales.isPending.value" :lines="6" />
      <template v-else>
        <OrdersChart :days="chartDays" />
        <!-- The same numbers for screen readers, which cannot read a canvas. -->
        <table class="sr-only">
          <caption>{{ t('admin.dashboard.chartTitle') }}</caption>
          <tbody>
            <tr v-for="day in chartDays" :key="day.label">
              <th scope="row">{{ day.label }}</th>
              <td>{{ day.count }}</td>
            </tr>
          </tbody>
        </table>
      </template>
    </section>

    <section class="rounded-card border border-line bg-surface p-4 sm:p-5">
      <h2 class="mb-3 font-bold text-ink">{{ t('admin.dashboard.byStatus') }}</h2>
      <ErrorState
        v-if="statusCounts.isError.value"
        :error="statusCounts.error.value"
        retryable
        @retry="statusCounts.refetch()"
      />
      <SkeletonBlock v-else-if="statusCounts.isPending.value" :lines="6" />
      <ul v-else class="divide-y divide-line" data-testid="orders-by-status">
        <li v-for="item in statusCounts.data.value" :key="item.status">
          <RouterLink
            :to="{ name: 'orders', query: { status: item.status } }"
            class="flex items-center justify-between gap-3 py-2 hover:text-primary"
          >
            <StatusTag kind="orderStatus" :value="item.status" />
            <span class="font-semibold tabular-nums text-ink">{{ item.count }}</span>
          </RouterLink>
        </li>
      </ul>
    </section>
  </div>

  <section
    v-if="canReports && topProducts.data.value?.items.length"
    class="mt-6 rounded-card border border-line bg-surface p-4 sm:p-5"
  >
    <h2 class="mb-3 font-bold text-ink">{{ t('admin.dashboard.topProducts') }}</h2>
    <ol class="divide-y divide-line" data-testid="top-products">
      <li
        v-for="item in topProducts.data.value.items"
        :key="item.productId"
        class="flex items-center justify-between gap-3 py-2"
      >
        <span class="min-w-0 truncate text-ink">{{ item.name }}</span>
        <span class="shrink-0 font-semibold tabular-nums text-ink">
          {{ t('admin.dashboard.topProductUnits', { units: item.units }) }}
        </span>
      </li>
    </ol>
  </section>

  <section v-if="canOrders" class="mt-6">
    <div class="mb-3 flex items-center justify-between">
      <h2 class="font-bold text-ink">{{ t('admin.dashboard.recentOrders') }}</h2>
      <RouterLink :to="{ name: 'orders' }" class="text-sm font-semibold text-primary">
        {{ t('admin.dashboard.allOrders') }}
      </RouterLink>
    </div>
    <AppTable
      :columns="recentColumns"
      :rows="recentOrders.data.value?.items ?? []"
      :loading="recentOrders.isPending.value"
      :error="recentOrders.error.value"
      :empty-title="t('admin.dashboard.noOrders')"
      @retry="recentOrders.refetch()"
    >
      <template #cell-number="{ row }">
        <RouterLink
          :to="{ name: 'order-detail', params: { id: row.id } }"
          class="font-semibold text-primary"
        >
          {{ row.number }}
        </RouterLink>
      </template>
      <template #cell-contactName="{ row }">{{ privacy.name(row.contactName) }}</template>
      <template #cell-status="{ row }">
        <StatusTag kind="orderStatus" :value="row.status" />
      </template>
      <template #cell-total="{ row }">{{ format.money(row.total, row.currency) }}</template>
      <template #cell-placedAt="{ row }">{{ format.dateTime(row.placedAt) }}</template>
    </AppTable>
  </section>
</template>
