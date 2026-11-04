<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { useApi, useAuthStore } from '@comtest/auth'
import { AppTable, ErrorState, PageHeader, SkeletonBlock, type TableColumn } from '@comtest/ui'
import { keepPreviousData, useQuery } from '@tanstack/vue-query'
import { computed, defineAsyncComponent, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { Permission } from '@/app/permissions'
import { STORE_OFFSET_MINUTES, dayBoundary, storeDate } from '@/features/dashboard/days'
import { useFormat } from '@/shared/composables/useFormat'
import ExportButtons from '../components/ExportButtons.vue'

const OrdersChart = defineAsyncComponent(
  () => import('@/features/dashboard/components/OrdersChart.vue'),
)

const DEFAULT_DAYS = 30
const DAY_MS = 86_400_000

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const format = useFormat()
const route = useRoute()
const router = useRouter()

const canSeeStock = computed(() => auth.can(Permission.InventoryRead))
const canSeeProfit = computed(() => auth.can(Permission.ReportsFinancial))

const today = storeDate()
const monthAgo = storeDate(new Date(Date.now() - (DEFAULT_DAYS - 1) * DAY_MS))

function queryDate(value: unknown, fallback: string): string {
  return typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : fallback
}

// The address holds the period, so that a report can be reloaded and sent to a colleague.
const fromDay = computed({
  get: () => queryDate(route.query.from, monthAgo),
  set: (from: string) => void router.replace({ query: { ...route.query, from } }),
})

const toDay = computed({
  get: () => queryDate(route.query.to, today),
  set: (to: string) => void router.replace({ query: { ...route.query, to } }),
})

const sort = ref<'revenue' | 'units'>('revenue')
const from = computed(() => dayBoundary(fromDay.value, 'start'))
const to = computed(() => dayBoundary(toDay.value, 'end'))

const sales = useQuery({
  queryKey: ['admin', 'reports', 'sales', from, to],
  placeholderData: keepPreviousData,
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/reports/sales', {
        params: {
          query: { from: from.value, to: to.value, utcOffsetMinutes: STORE_OFFSET_MINUTES },
        },
      }),
    ),
})

const products = useQuery({
  queryKey: ['admin', 'reports', 'products', from, to, sort],
  placeholderData: keepPreviousData,
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/reports/products', {
        params: { query: { from: from.value, to: to.value, sort: sort.value, limit: 20 } },
      }),
    ),
})

const customers = useQuery({
  queryKey: ['admin', 'reports', 'customers', from, to],
  placeholderData: keepPreviousData,
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/reports/customers', {
        params: { query: { from: from.value, to: to.value, limit: 10 } },
      }),
    ),
})

const stock = useQuery({
  queryKey: ['admin', 'reports', 'stock'],
  enabled: canSeeStock,
  queryFn: () => unwrap(api.client.GET('/api/v1/admin/reports/stock')),
})

const currency = computed(() => sales.data.value?.currency ?? 'TMT')

const figures = computed(() => {
  const report = sales.data.value

  if (!report) {
    return []
  }

  const money = (amount: number) => format.money(amount, report.currency)

  return [
    {
      key: 'revenue',
      label: t('admin.reports.revenue'),
      value: money(report.figures.revenue),
      previous: money(report.previous.revenue),
    },
    {
      key: 'orders',
      label: t('admin.reports.orders'),
      value: String(report.figures.orders),
      previous: String(report.previous.orders),
    },
    {
      key: 'average',
      label: t('admin.reports.average'),
      value: money(report.figures.averageOrderValue),
      previous: money(report.previous.averageOrderValue),
    },
    {
      key: 'units',
      label: t('admin.reports.units'),
      value: String(report.figures.units),
      previous: String(report.previous.units),
    },
  ]
})

const chartDays = computed(() =>
  (sales.data.value?.days ?? []).map((day) => ({
    label: `${day.date.slice(8, 10)}.${day.date.slice(5, 7)}`,
    count: day.orders,
    revenue: day.revenue,
  })),
)

const productColumns = computed<TableColumn[]>(() => [
  { key: 'name', label: t('admin.reports.product'), minWidth: 220 },
  { key: 'units', label: t('admin.reports.units'), minWidth: 110, align: 'right' },
  { key: 'revenue', label: t('admin.reports.revenue'), minWidth: 140, align: 'right' },
  ...(canSeeProfit.value
    ? [
        {
          key: 'estimatedProfit',
          label: t('admin.reports.profit'),
          minWidth: 160,
          align: 'right' as const,
        },
      ]
    : []),
])

const customerColumns = computed<TableColumn[]>(() => [
  { key: 'name', label: t('admin.reports.customer'), minWidth: 180 },
  { key: 'phone', label: t('admin.reports.phone'), minWidth: 150 },
  { key: 'orders', label: t('admin.reports.orders'), minWidth: 100, align: 'right' },
  { key: 'revenue', label: t('admin.reports.revenue'), minWidth: 140, align: 'right' },
])

// Keeps a typed-in end from landing before the start, which the API would refuse.
watch([fromDay, toDay], ([start, end]) => {
  if (start > end) {
    toDay.value = start
  }
})
</script>

<template>
  <PageHeader :title="t('admin.nav.reports')" :description="t('admin.reports.intro')">
    <template #actions>
      <el-date-picker
        v-model="fromDay"
        class="!w-40"
        type="date"
        value-format="YYYY-MM-DD"
        :clearable="false"
        :placeholder="t('admin.reports.from')"
        :aria-label="t('admin.reports.from')"
        data-testid="report-from"
      />
      <el-date-picker
        v-model="toDay"
        class="!w-40"
        type="date"
        value-format="YYYY-MM-DD"
        :clearable="false"
        :placeholder="t('admin.reports.to')"
        :aria-label="t('admin.reports.to')"
        data-testid="report-to"
      />
    </template>
  </PageHeader>

  <section class="mb-6">
    <ErrorState
      v-if="sales.isError.value"
      :error="sales.error.value"
      retryable
      @retry="sales.refetch()"
    />
    <SkeletonBlock v-else-if="sales.isPending.value" :lines="4" />
    <template v-else>
      <ul class="grid grid-cols-2 gap-3 xl:grid-cols-4" data-testid="report-figures">
        <li
          v-for="figure in figures"
          :key="figure.key"
          class="rounded-card border border-line bg-surface p-4"
          :data-testid="`figure-${figure.key}`"
        >
          <p class="text-sm font-medium text-ink-muted">{{ figure.label }}</p>
          <p class="mt-1 text-2xl font-bold tabular-nums text-ink" data-testid="figure-value">
            {{ figure.value }}
          </p>
          <p class="mt-1 text-xs text-ink-subtle">
            {{ t('admin.reports.previous', { value: figure.previous }) }}
          </p>
        </li>
      </ul>
      <p v-if="sales.data.value" class="mt-3 text-sm text-ink-muted" data-testid="report-refunded">
        {{
          t('admin.reports.refunded', {
            amount: format.money(sales.data.value.refunded, sales.data.value.currency),
          })
        }}
      </p>
    </template>
  </section>

  <section class="mb-6 rounded-card border border-line bg-surface p-4 sm:p-5">
    <div class="mb-3 flex flex-wrap items-center justify-between gap-3">
      <div>
        <h2 class="font-bold text-ink">{{ t('admin.reports.byDay') }}</h2>
        <p class="text-sm text-ink-muted">{{ t('admin.reports.byDayHint') }}</p>
      </div>
      <ExportButtons kind="sales" :from="from" :to="to" />
    </div>
    <OrdersChart v-if="chartDays.length" :days="chartDays" />
    <!-- The same numbers for screen readers, which cannot read a canvas. -->
    <table class="sr-only">
      <caption>
        {{ t('admin.reports.byDay') }}
      </caption>
      <tbody>
        <tr v-for="day in chartDays" :key="day.label">
          <th scope="row">{{ day.label }}</th>
          <td>{{ day.count }}</td>
          <td>{{ format.money(day.revenue, currency) }}</td>
        </tr>
      </tbody>
    </table>
  </section>

  <section class="mb-6">
    <div class="mb-3 flex flex-wrap items-center justify-between gap-3">
      <div>
        <h2 class="font-bold text-ink">{{ t('admin.reports.products') }}</h2>
        <p v-if="canSeeProfit" class="text-sm text-ink-muted">
          {{ t('admin.reports.profitHint') }}
        </p>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <el-select
          v-model="sort"
          class="!w-48"
          :aria-label="t('admin.reports.sortBy')"
          data-testid="report-sort"
        >
          <el-option value="revenue" :label="t('admin.reports.byRevenue')" />
          <el-option value="units" :label="t('admin.reports.byUnits')" />
        </el-select>
        <ExportButtons kind="products" :from="from" :to="to" />
      </div>
    </div>
    <AppTable
      :columns="productColumns"
      :rows="products.data.value?.items ?? []"
      :loading="products.isPending.value"
      :error="products.error.value"
      :empty-title="t('admin.reports.noSales')"
      @retry="products.refetch()"
    >
      <template #cell-revenue="{ row }">{{ format.money(row.revenue, currency) }}</template>
      <template #cell-estimatedProfit="{ row }">
        {{ row.estimatedProfit === null ? '—' : format.money(row.estimatedProfit, currency) }}
      </template>
    </AppTable>
  </section>

  <section class="mb-6">
    <div class="mb-3 flex flex-wrap items-center justify-between gap-3">
      <div>
        <h2 class="font-bold text-ink">{{ t('admin.reports.customers') }}</h2>
        <p v-if="customers.data.value" class="text-sm text-ink-muted" data-testid="report-buyers">
          {{
            t('admin.reports.buyersSummary', {
              total: customers.data.value.buyers,
              fresh: customers.data.value.newBuyers,
              returning: customers.data.value.returningBuyers,
            })
          }}
        </p>
      </div>
      <ExportButtons kind="customers" :from="from" :to="to" />
    </div>
    <AppTable
      :columns="customerColumns"
      :rows="customers.data.value?.top ?? []"
      :loading="customers.isPending.value"
      :error="customers.error.value"
      :empty-title="t('admin.reports.noSales')"
      @retry="customers.refetch()"
    >
      <template #cell-revenue="{ row }">{{ format.money(row.revenue, currency) }}</template>
    </AppTable>
  </section>

  <section v-if="canSeeStock" class="rounded-card border border-line bg-surface p-4 sm:p-5">
    <div class="mb-3 flex flex-wrap items-center justify-between gap-3">
      <div>
        <h2 class="font-bold text-ink">{{ t('admin.reports.stock') }}</h2>
        <p class="text-sm text-ink-muted">{{ t('admin.reports.stockHint') }}</p>
      </div>
      <ExportButtons kind="stock" />
    </div>
    <ErrorState
      v-if="stock.isError.value"
      :error="stock.error.value"
      retryable
      @retry="stock.refetch()"
    />
    <SkeletonBlock v-else-if="stock.isPending.value" :lines="3" />
    <dl
      v-else-if="stock.data.value"
      class="grid grid-cols-2 gap-4 sm:grid-cols-3 xl:grid-cols-5"
      data-testid="report-stock"
    >
      <div>
        <dt class="text-sm text-ink-muted">{{ t('admin.reports.stockOnHand') }}</dt>
        <dd class="text-xl font-bold tabular-nums text-ink">{{ stock.data.value.onHand }}</dd>
      </div>
      <div>
        <dt class="text-sm text-ink-muted">{{ t('admin.reports.stockReserved') }}</dt>
        <dd class="text-xl font-bold tabular-nums text-ink">{{ stock.data.value.reserved }}</dd>
      </div>
      <div>
        <dt class="text-sm text-ink-muted">{{ t('admin.reports.stockLow') }}</dt>
        <dd class="text-xl font-bold tabular-nums text-ink">{{ stock.data.value.lowStock }}</dd>
      </div>
      <div>
        <dt class="text-sm text-ink-muted">{{ t('admin.reports.stockOut') }}</dt>
        <dd class="text-xl font-bold tabular-nums text-ink">{{ stock.data.value.outOfStock }}</dd>
      </div>
      <div v-if="stock.data.value.stockValue !== null">
        <dt class="text-sm text-ink-muted">{{ t('admin.reports.stockValue') }}</dt>
        <dd class="text-xl font-bold tabular-nums text-ink" data-testid="report-stock-value">
          {{ format.money(stock.data.value.stockValue ?? 0, stock.data.value.currency) }}
        </dd>
      </div>
    </dl>
  </section>
</template>
