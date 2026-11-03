<script setup lang="ts" generic="Row extends object">
import ErrorState from './ErrorState.vue'
import EmptyState from './EmptyState.vue'
import SkeletonBlock from './SkeletonBlock.vue'

export interface TableColumn {
  key: string
  label: string
  width?: number | string
  minWidth?: number | string
  align?: 'left' | 'center' | 'right'
}

withDefaults(
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
  }>(),
  { rowKey: 'id' },
)

defineEmits<{
  retry: []
  'update:page': [page: number]
  'update:pageSize': [pageSize: number]
}>()

const cellValue = (row: Row, key: string) => (row as Record<string, unknown>)[key]

defineSlots<{ [name: `cell-${string}`]: (props: { row: Row }) => unknown }>()
</script>

<template>
  <div class="overflow-hidden rounded-card border border-line bg-surface">
    <ErrorState v-if="error" :error="error" retryable @retry="$emit('retry')" />
    <div v-else-if="loading && rows.length === 0" class="p-6"><SkeletonBlock :lines="6" /></div>
    <EmptyState v-else-if="rows.length === 0" :title="emptyTitle" />
    <template v-else>
      <el-table v-loading="loading" :data="rows" :row-key="rowKey" class="w-full">
        <el-table-column
          v-for="column in columns"
          :key="column.key"
          :prop="column.key"
          :label="column.label"
          :width="column.width"
          :min-width="column.minWidth"
          :align="column.align"
        >
          <template #default="{ row }">
            <slot :name="`cell-${column.key}`" :row="row">
              {{ cellValue(row, column.key) }}
            </slot>
          </template>
        </el-table-column>
      </el-table>
      <div v-if="total !== undefined" class="flex justify-end border-t border-line p-3">
        <el-pagination
          :total="total"
          :current-page="page"
          :page-size="pageSize"
          layout="total, prev, pager, next"
          background
          @update:current-page="$emit('update:page', $event)"
          @update:page-size="$emit('update:pageSize', $event)"
        />
      </div>
    </template>
  </div>
</template>
