<script setup lang="ts" generic="Row extends object">
import { AppButton, AppTable, usePreference, type TableColumn } from '@comtest/ui'
import { Operation } from '@element-plus/icons-vue'
import { computed, ref, useSlots } from 'vue'
import { useI18n } from 'vue-i18n'

export interface DataColumn extends TableColumn {
  /** The column chooser cannot hide this one. */
  required?: boolean
  /** Starts hidden, until the user switches it on. */
  optional?: boolean
}

const props = defineProps<{
  /** Names this table's remembered column choice. */
  id: string
  columns: DataColumn[]
  rows: Row[]
  rowKey?: string
  loading?: boolean
  error?: unknown
  total: number
  page: number
  pageSize: number
  sort?: string
  selectable?: boolean
  emptyTitle?: string
  hasFilters?: boolean
}>()

const emit = defineEmits<{
  retry: []
  'update:page': [page: number]
  'update:pageSize': [pageSize: number]
  'update:sort': [sort: string]
  'selection-change': [rows: Row[]]
  'clear-filters': []
}>()

defineSlots<{
  [name: `cell-${string}`]: (props: { row: Row }) => unknown
  filters?: () => unknown
  actions?: () => unknown
  empty?: () => unknown
}>()

const { t } = useI18n({ useScope: 'global' })
const slots = useSlots()

/** Hidden columns, by key. A stored choice survives a release that adds a new column. */
const stored = usePreference(`table.${props.id}.columns`, '')

const defaultHidden = computed(() =>
  props.columns.filter((column) => column.optional).map((column) => column.key),
)

const hidden = computed<string[]>({
  get: () => {
    if (!stored.value) {
      return defaultHidden.value
    }

    try {
      const parsed: unknown = JSON.parse(stored.value)

      return Array.isArray(parsed) ? parsed.map(String) : defaultHidden.value
    } catch {
      return defaultHidden.value
    }
  },
  set: (value) => {
    stored.value = JSON.stringify(value)
  },
})

const chooserColumns = computed(() => props.columns.filter((column) => !column.required))
const shownColumns = computed(() =>
  props.columns.filter((column) => column.required || !hidden.value.includes(column.key)),
)
const slottedColumns = computed(() =>
  shownColumns.value.map((column) => column.key).filter((key) => slots[`cell-${key}`]),
)

const inner = ref<{ clearSelection: () => void } | null>(null)

defineExpose({ clearSelection: () => inner.value?.clearSelection() })

function toggle(key: string, visible: boolean | string | number): void {
  hidden.value = visible ? hidden.value.filter((item) => item !== key) : [...hidden.value, key]
}
</script>

<template>
  <section class="space-y-3">
    <div class="flex flex-wrap items-end gap-2" data-testid="table-toolbar">
      <div class="flex min-w-0 flex-1 flex-wrap items-end gap-2">
        <slot name="filters" />
        <AppButton v-if="hasFilters" variant="ghost" @click="emit('clear-filters')">
          {{ t('admin.table.clearFilters') }}
        </AppButton>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <slot name="actions" />
        <el-popover
          v-if="chooserColumns.length"
          trigger="click"
          placement="bottom-end"
          :width="240"
        >
          <template #reference>
            <el-button :icon="Operation" data-testid="column-chooser">
              {{ t('admin.table.columns') }}
            </el-button>
          </template>
          <p class="mb-1 text-sm font-semibold text-ink">{{ t('admin.table.columnsTitle') }}</p>
          <el-checkbox
            v-for="column in chooserColumns"
            :key="column.key"
            class="!flex"
            :model-value="!hidden.includes(column.key)"
            :label="column.label"
            :data-testid="`column-${column.key}`"
            @update:model-value="toggle(column.key, $event)"
          />
        </el-popover>
      </div>
    </div>

    <AppTable
      ref="inner"
      :columns="shownColumns"
      :rows="rows"
      :row-key="rowKey"
      :loading="loading"
      :error="error"
      :empty-title="emptyTitle"
      :total="total"
      :page="page"
      :page-size="pageSize"
      :sort="sort"
      :selectable="selectable"
      page-size-choice
      @retry="emit('retry')"
      @update:page="emit('update:page', $event)"
      @update:page-size="emit('update:pageSize', $event)"
      @update:sort="emit('update:sort', $event)"
      @selection-change="emit('selection-change', $event)"
    >
      <template v-for="key in slottedColumns" :key="key" #[`cell-${key}`]="{ row }">
        <slot :name="`cell-${key}`" :row="row" />
      </template>
      <template v-if="$slots.empty" #empty><slot name="empty" /></template>
    </AppTable>
  </section>
</template>
