<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { PageHeader } from '@comtest/ui'
import { Delete, Edit, Plus, Search } from '@element-plus/icons-vue'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/vue-query'
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import DataTable, { type DataColumn } from '@/shared/components/DataTable.vue'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useDrawerItem } from '@/shared/composables/useDrawerItem'
import { useFormat } from '@/shared/composables/useFormat'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { toastSuccess } from '@/shared/toast'
import PromotionForm from '../components/PromotionForm.vue'

type Promotion = Schema<'PromotionDto'>

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const format = useFormat()
const confirm = useConfirm()
const queryClient = useQueryClient()

const table = useTableQuery({ filters: { q: '' } })
const search = ref(table.filters.q)
const drawer = useDrawerItem<Promotion>()

watch(
  () => table.filters.q,
  (value) => {
    search.value = value
  },
)

const promotions = useQuery({
  queryKey: [
    'admin',
    'promotions',
    computed(() => ({ page: table.page, pageSize: table.pageSize, q: table.filters.q })),
  ],
  placeholderData: keepPreviousData,
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/promotions', {
        params: {
          query: { page: table.page, pageSize: table.pageSize, q: table.filters.q || undefined },
        },
      }),
    ),
})

const columns = computed<DataColumn[]>(() => [
  { key: 'name', label: t('admin.common.name'), minWidth: 200 },
  { key: 'code', label: t('admin.promotions.code'), minWidth: 160 },
  { key: 'discount', label: t('admin.promotions.discount'), minWidth: 140 },
  { key: 'period', label: t('admin.promotions.period'), minWidth: 210 },
  { key: 'usage', label: t('admin.promotions.usage'), minWidth: 110, align: 'right' },
  { key: 'isActive', label: t('admin.promotions.state'), minWidth: 110 },
  { key: 'actions', label: '', width: 110, align: 'right' },
])

function describeDiscount(row: Promotion): string {
  if (row.type === 'freeShipping') {
    return t('admin.promotions.types.freeShipping')
  }

  return row.type === 'percentage' ? `${row.value}%` : format.money(row.value, row.currency)
}

function describePeriod(row: Promotion): string {
  if (!row.startsAt && !row.endsAt) {
    return t('admin.promotions.noPeriod')
  }

  return [row.startsAt, row.endsAt].map((value) => (value ? format.date(value) : '…')).join(' – ')
}

function remove(row: Promotion): void {
  confirm.ask({
    title: t('admin.promotions.delete'),
    message: t('admin.promotions.deleteConfirm', { name: row.name }),
    confirmText: t('common.delete'),
    danger: true,
    action: async () => {
      await unwrap(
        api.client.DELETE('/api/v1/admin/promotions/{id}', { params: { path: { id: row.id } } }),
      )
      await queryClient.invalidateQueries({ queryKey: ['admin', 'promotions'] })
      toastSuccess(t('admin.promotions.deleted'))
    },
  })
}
</script>

<template>
  <PageHeader :title="t('admin.nav.promotions')" :description="t('admin.promotions.intro')">
    <template #actions>
      <el-button type="primary" :icon="Plus" data-testid="new-promotion" @click="drawer.show()">
        {{ t('admin.promotions.new') }}
      </el-button>
    </template>
  </PageHeader>

  <DataTable
    id="promotions"
    v-model:page="table.page"
    v-model:page-size="table.pageSize"
    :columns="columns"
    :rows="promotions.data.value?.items ?? []"
    :total="promotions.data.value?.totalCount ?? 0"
    :loading="promotions.isFetching.value"
    :error="promotions.error.value"
    :has-filters="table.hasFilters"
    :empty-title="t(table.hasFilters ? 'admin.promotions.noMatches' : 'admin.promotions.empty')"
    @retry="promotions.refetch()"
    @clear-filters="table.clearFilters()"
  >
    <template #filters>
      <el-input
        v-model="search"
        class="!w-full sm:!w-64"
        clearable
        :prefix-icon="Search"
        :placeholder="t('admin.promotions.searchHint')"
        :aria-label="t('admin.promotions.searchHint')"
        data-testid="filter-search"
        @keyup.enter="table.filters.q = search.trim()"
        @clear="table.filters.q = ''"
        @blur="table.filters.q = search.trim()"
      />
    </template>
    <template #cell-name="{ row }">
      <span class="font-semibold text-ink" data-testid="promotion-row">{{ row.name }}</span>
    </template>
    <template #cell-code="{ row }">
      <code v-if="row.code" class="font-mono text-sm">{{ row.code }}</code>
      <span v-else class="text-ink-muted">{{ t('admin.promotions.automatic') }}</span>
    </template>
    <template #cell-discount="{ row }">{{ describeDiscount(row) }}</template>
    <template #cell-period="{ row }">{{ describePeriod(row) }}</template>
    <template #cell-usage="{ row }">
      <span class="tabular-nums" data-testid="promotion-usage">
        {{ row.usageCount }}{{ row.usageLimit ? ` / ${row.usageLimit}` : '' }}
      </span>
    </template>
    <template #cell-isActive="{ row }">
      {{ t(row.isActive ? 'admin.promotions.on' : 'admin.promotions.off') }}
    </template>
    <template #cell-actions="{ row }">
      <el-button
        :icon="Edit"
        text
        size="small"
        :aria-label="t('admin.common.edit')"
        data-testid="promotion-edit"
        @click="drawer.show(row)"
      />
      <el-button
        :icon="Delete"
        text
        size="small"
        type="danger"
        :aria-label="t('common.delete')"
        data-testid="promotion-delete"
        @click="remove(row)"
      />
    </template>
  </DataTable>

  <PromotionForm
    v-if="drawer.mounted.value"
    :key="drawer.session.value"
    v-model="drawer.open.value"
    :promotion="drawer.item.value"
    @closed="drawer.closed()"
  />
</template>
