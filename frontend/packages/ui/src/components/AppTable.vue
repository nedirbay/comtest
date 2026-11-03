<script setup lang="ts" generic="Row extends object">
import { nextTick, ref, watch } from 'vue'
import ErrorState from './ErrorState.vue'
import EmptyState from './EmptyState.vue'
import SkeletonBlock from './SkeletonBlock.vue'

export interface TableColumn {
  key: string
  label: string
  width?: number | string
  minWidth?: number | string
  align?: 'left' | 'center' | 'right'
  /** The server can order by this column. The table only reports the request. */
  sortable?: boolean
}

const PAGE_SIZES = [20, 50, 100]

const props = withDefaults(
  defineProps<{
    columns: TableColumn[]
    rows: Row[]
    rowKey?: string
    loading?: boolean
    error?: unknown
    emptyTitle?: string
    /** Set these for server-side paging. The table shows pages but never slices data itself. */
    total?: number
    page?: number
    pageSize?: number
    /** `name` or `-name`, in the API's sort format. */
    sort?: string
    /** Adds a checkbox column. The chosen rows come through `selection-change`. */
    selectable?: boolean
    /** Offers a choice of page sizes next to the pager. */
    pageSizeChoice?: boolean
  }>(),
  { rowKey: 'id' },
)

const emit = defineEmits<{
  retry: []
  'update:page': [page: number]
  'update:pageSize': [pageSize: number]
  'update:sort': [sort: string]
  'selection-change': [rows: Row[]]
}>()

const cellValue = (row: Row, key: string) => (row as Record<string, unknown>)[key]

defineSlots<{
  [name: `cell-${string}`]: (props: { row: Row }) => unknown
  empty?: () => unknown
}>()

interface SortParts {
  prop: string
  order: 'ascending' | 'descending'
}

function parseSort(sort: string | undefined): SortParts | undefined {
  if (!sort) {
    return undefined
  }

  return sort.startsWith('-')
    ? { prop: sort.slice(1), order: 'descending' }
    : { prop: sort, order: 'ascending' }
}

const table = ref<{
  sort: (prop: string, order: string) => void
  clearSelection: () => void
} | null>(null)

defineExpose({ clearSelection: () => table.value?.clearSelection() })

function onSortChange({ prop, order }: { prop: string | null; order: string | null }): void {
  const next = !prop || !order ? '' : order === 'descending' ? `-${prop}` : prop

  if (next !== (props.sort ?? '')) {
    emit('update:sort', next)
  }
}

// Going back in the browser changes the sort from outside, and the arrows in the header must follow.
watch(
  () => props.sort,
  async (next) => {
    const parsed = parseSort(next)

    await nextTick()

    if (parsed) {
      table.value?.sort(parsed.prop, parsed.order)
    }
  },
)
</script>

<template>
  <div class="overflow-hidden rounded-card border border-line bg-surface">
    <ErrorState v-if="error" :error="error" retryable @retry="$emit('retry')" />
    <div v-else-if="loading && rows.length === 0" class="p-6"><SkeletonBlock :lines="6" /></div>
    <template v-else>
      <EmptyState v-if="rows.length === 0" :title="emptyTitle">
        <template v-if="$slots.empty" #default><slot name="empty" /></template>
      </EmptyState>
      <el-table
        v-else
        ref="table"
        v-loading="loading"
        :data="rows"
        :row-key="rowKey"
        :default-sort="parseSort(sort)"
        class="w-full"
        @sort-change="onSortChange"
        @selection-change="$emit('selection-change', $event)"
      >
        <el-table-column v-if="selectable" type="selection" width="48" reserve-selection />
        <el-table-column
          v-for="column in columns"
          :key="column.key"
          :prop="column.key"
          :label="column.label"
          :width="column.width"
          :min-width="column.minWidth"
          :align="column.align"
          :sortable="column.sortable ? 'custom' : false"
        >
          <!-- Element Plus also calls this slot once with an empty row, to look for nested columns. -->
          <template #default="{ row, $index }">
            <slot v-if="$index >= 0" :name="`cell-${column.key}`" :row="row">
              {{ cellValue(row, column.key) }}
            </slot>
          </template>
        </el-table-column>
      </el-table>
      <div
        v-if="total !== undefined && (total > 0 || rows.length > 0)"
        class="flex justify-end overflow-x-auto border-t border-line p-3"
      >
        <el-pagination
          :total="total"
          :current-page="page"
          :page-size="pageSize"
          :page-sizes="PAGE_SIZES"
          :pager-count="5"
          :layout="pageSizeChoice ? 'total, sizes, prev, pager, next' : 'total, prev, pager, next'"
          background
          @update:current-page="$emit('update:page', $event)"
          @update:page-size="$emit('update:pageSize', $event)"
        />
      </div>
    </template>
  </div>
</template>
