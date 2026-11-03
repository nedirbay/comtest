<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi, useAuthStore } from '@comtest/auth'
import { ErrorState, normalizeStatus, PageHeader, SkeletonBlock, EmptyState } from '@comtest/ui'
import { useInfiniteQuery, useQuery, type InfiniteData, type QueryKey } from '@tanstack/vue-query'
import { computed, h } from 'vue'
import { useI18n } from 'vue-i18n'
import { Permission } from '@/app/permissions'
import { useFormat } from '@/shared/composables/useFormat'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { MOVEMENTS_KEY, STOCK_KEY, useWarehouses } from '../queries'

type Movement = Schema<'StockMovementDto'>
type MovementPage = Schema<'PagedResultOfStockMovementDto'>

/** The API's largest page. The list only asks for more when the user scrolls near its end. */
const PAGE_SIZE = 100
/** Enough rows to name the variants of a normal store. See the note in the report on this. */
const NAME_LOOKUP_PAGES = 5
const ROW_HEIGHT = 44
/** Room for the taller header row and a horizontal scrollbar. */
const HEADER_EXTRA = 24

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const format = useFormat()
const warehouses = useWarehouses()

const table = useTableQuery({ filters: { warehouseId: '', variantId: '' } })

const movements = useInfiniteQuery<
  MovementPage,
  Error,
  InfiniteData<MovementPage>,
  QueryKey,
  number
>({
  queryKey: [
    ...MOVEMENTS_KEY,
    computed(() => ({
      warehouseId: table.filters.warehouseId,
      variantId: table.filters.variantId,
    })),
  ],
  initialPageParam: 1,
  getNextPageParam: (last) =>
    last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined,
  queryFn: ({ pageParam }) =>
    unwrap(
      api.client.GET('/api/v1/admin/inventory/movements', {
        params: {
          query: {
            page: pageParam,
            pageSize: PAGE_SIZE,
            warehouseId: table.filters.warehouseId || undefined,
            variantId: table.filters.variantId || undefined,
          },
        },
      }),
    ),
})

const rows = computed<Movement[]>(
  () => movements.data.value?.pages.flatMap((page) => page.items) ?? [],
)
const totalCount = computed(() => movements.data.value?.pages[0]?.totalCount ?? 0)

// A movement names its variant only by id, and the stock list is the one place with both.
const variantNames = useQuery({
  queryKey: [...STOCK_KEY, 'names'],
  enabled: computed(() => auth.can(Permission.InventoryRead)),
  queryFn: async () => {
    const names = new Map<string, string>()

    for (let page = 1; page <= NAME_LOOKUP_PAGES; page += 1) {
      const result = await unwrap(
        api.client.GET('/api/v1/admin/inventory/stock', {
          params: { query: { page, pageSize: PAGE_SIZE } },
        }),
      )

      for (const item of result.items) {
        names.set(item.variantId, [item.productName, item.sku].filter(Boolean).join(' · '))
      }

      if (page * PAGE_SIZE >= result.totalCount) {
        break
      }
    }

    return names
  },
})

const warehouseCode = (id: string) =>
  warehouses.data.value?.find((warehouse) => warehouse.id === id)?.code ?? id.slice(0, 8)

const variantName = (id: string) => variantNames.data.value?.get(id) ?? id.slice(0, 8)

function signed(value: number): string {
  return value > 0 ? `+${value}` : String(value)
}

const typeLabel = (type: string) => {
  const key = `admin.movements.type_${normalizeStatus(type)}`

  return t(key)
}

const columns = computed(() => [
  {
    key: 'at',
    dataKey: 'at',
    title: t('admin.movements.at'),
    width: 175,
    cellRenderer: ({ rowData }: { rowData: Movement }) => h('span', format.dateTime(rowData.at)),
  },
  {
    key: 'type',
    dataKey: 'type',
    title: t('admin.movements.type'),
    width: 120,
    cellRenderer: ({ rowData }: { rowData: Movement }) => h('span', typeLabel(rowData.type)),
  },
  {
    key: 'variant',
    dataKey: 'variantId',
    title: t('admin.movements.variant'),
    width: 250,
    cellRenderer: ({ rowData }: { rowData: Movement }) =>
      h(
        'span',
        { class: 'truncate', title: variantName(rowData.variantId) },
        variantName(rowData.variantId),
      ),
  },
  {
    key: 'warehouse',
    dataKey: 'warehouseId',
    title: t('admin.stock.warehouse'),
    width: 90,
    cellRenderer: ({ rowData }: { rowData: Movement }) =>
      h('span', warehouseCode(rowData.warehouseId)),
  },
  {
    key: 'onHandDelta',
    dataKey: 'onHandDelta',
    title: t('admin.movements.onHandDelta'),
    width: 80,
    align: 'right' as const,
    cellRenderer: ({ rowData }: { rowData: Movement }) =>
      h('span', { class: 'tabular-nums font-semibold' }, signed(rowData.onHandDelta)),
  },
  {
    key: 'reservedDelta',
    dataKey: 'reservedDelta',
    title: t('admin.movements.reservedDelta'),
    width: 110,
    align: 'right' as const,
    cellRenderer: ({ rowData }: { rowData: Movement }) =>
      h('span', { class: 'tabular-nums' }, signed(rowData.reservedDelta)),
  },
  {
    key: 'onHandAfter',
    dataKey: 'onHandAfter',
    title: t('admin.movements.onHandAfter'),
    width: 120,
    align: 'right' as const,
  },
  {
    key: 'reason',
    dataKey: 'reason',
    title: t('admin.movements.reason'),
    width: 240,
    cellRenderer: ({ rowData }: { rowData: Movement }) =>
      h('span', { class: 'truncate', title: rowData.reason ?? '' }, rowData.reason ?? ''),
  },
])

/** Grows with the rows until it fills the screen. Past that, it scrolls inside its own box. */
const tableHeight = computed(
  () => `min(70dvh, ${(rows.value.length + 1) * ROW_HEIGHT + HEADER_EXTRA}px)`,
)

function loadMore(): void {
  if (movements.hasNextPage.value && !movements.isFetchingNextPage.value) {
    void movements.fetchNextPage()
  }
}
</script>

<template>
  <PageHeader :title="t('admin.nav.movements')" :description="t('admin.movements.intro')" />

  <div class="mb-3 flex flex-wrap items-center gap-2" data-testid="table-toolbar">
    <el-select
      v-model="table.filters.warehouseId"
      class="!w-full sm:!w-48"
      clearable
      value-on-clear=""
      :empty-values="['', null, undefined]"
      :placeholder="t('admin.stock.allWarehouses')"
      :aria-label="t('admin.stock.warehouse')"
    >
      <el-option
        v-for="warehouse in warehouses.data.value"
        :key="warehouse.id"
        :value="warehouse.id"
        :label="warehouse.name"
      />
    </el-select>
    <el-tag v-if="table.filters.variantId" type="info" disable-transitions>
      {{ t('admin.stock.oneVariant') }}
    </el-tag>
    <el-button v-if="table.hasFilters" text @click="table.clearFilters()">
      {{ t('admin.table.clearFilters') }}
    </el-button>
    <span class="ml-auto text-sm text-ink-muted" data-testid="movements-count">
      {{ t('admin.movements.shown', { shown: rows.length, total: totalCount }) }}
    </span>
  </div>

  <div class="overflow-hidden rounded-card border border-line bg-surface">
    <ErrorState
      v-if="movements.isError.value"
      :error="movements.error.value"
      retryable
      @retry="movements.refetch()"
    />
    <div v-else-if="movements.isPending.value" class="p-6"><SkeletonBlock :lines="8" /></div>
    <EmptyState v-else-if="rows.length === 0" :title="t('admin.movements.empty')" />
    <div v-else :style="{ height: tableHeight }" data-testid="movements-table">
      <el-auto-resizer>
        <template #default="{ width, height }">
          <el-table-v2
            :columns="columns"
            :data="rows"
            :width="width"
            :height="height"
            :row-height="ROW_HEIGHT"
            row-key="id"
            fixed
            :end-reached-threshold="400"
            @end-reached="loadMore"
          />
        </template>
      </el-auto-resizer>
    </div>
  </div>
</template>
