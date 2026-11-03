<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi, useAuthStore } from '@comtest/auth'
import { AppButton, PageHeader } from '@comtest/ui'
import { Plus } from '@element-plus/icons-vue'
import { keepPreviousData, useQuery } from '@tanstack/vue-query'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'
import { Permission } from '@/app/permissions'
import DataTable, { type DataColumn } from '@/shared/components/DataTable.vue'
import { useDrawerItem } from '@/shared/composables/useDrawerItem'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import AdjustStockForm from '../components/AdjustStockForm.vue'
import ThresholdForm from '../components/ThresholdForm.vue'
import { STOCK_KEY, useWarehouses } from '../queries'

type StockRow = Schema<'StockItemDto'>

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const warehouses = useWarehouses()

const table = useTableQuery({ filters: { warehouseId: '', lowOnly: '', variantId: '' } })
const canWrite = computed(() => auth.can(Permission.InventoryWrite))
const adjust = useDrawerItem<StockRow>()
const threshold = useDrawerItem<StockRow>()

const lowOnly = computed<boolean>({
  get: () => table.filters.lowOnly === 'true',
  set: (value) => {
    table.filters.lowOnly = value ? 'true' : ''
  },
})

const stock = useQuery({
  queryKey: [
    ...STOCK_KEY,
    'list',
    computed(() => ({
      page: table.page,
      pageSize: table.pageSize,
      warehouseId: table.filters.warehouseId,
      lowOnly: table.filters.lowOnly,
      variantId: table.filters.variantId,
    })),
  ],
  placeholderData: keepPreviousData,
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/inventory/stock', {
        params: {
          query: {
            page: table.page,
            pageSize: table.pageSize,
            warehouseId: table.filters.warehouseId || undefined,
            variantId: table.filters.variantId || undefined,
            lowOnly: table.filters.lowOnly === 'true' ? true : undefined,
          },
        },
      }),
    ),
})

const columns = computed<DataColumn[]>(() => [
  { key: 'productName', label: t('admin.stock.product'), minWidth: 170, required: true },
  { key: 'sku', label: t('admin.productForm.sku'), minWidth: 130 },
  { key: 'warehouseCode', label: t('admin.stock.warehouse'), minWidth: 80 },
  { key: 'onHand', label: t('admin.stock.onHand'), minWidth: 70, align: 'right' },
  { key: 'reserved', label: t('admin.stock.reserved'), minWidth: 100, align: 'right' },
  { key: 'available', label: t('admin.stock.available'), minWidth: 95, align: 'right' },
  { key: 'lowStockThreshold', label: t('admin.stock.threshold'), minWidth: 65, align: 'right' },
  { key: 'actions', label: '', width: 240, align: 'right', required: true },
])
</script>

<template>
  <PageHeader :title="t('admin.nav.stock')" :description="t('admin.stock.intro')">
    <template #actions>
      <RouterLink :to="{ name: 'movements' }">
        <AppButton>{{ t('admin.nav.movements') }}</AppButton>
      </RouterLink>
      <el-button
        v-if="canWrite"
        type="primary"
        :icon="Plus"
        data-testid="adjust-stock"
        @click="adjust.show()"
      >
        {{ t('admin.stock.adjust') }}
      </el-button>
    </template>
  </PageHeader>

  <DataTable
    id="stock"
    v-model:page="table.page"
    v-model:page-size="table.pageSize"
    :columns="columns"
    :rows="stock.data.value?.items ?? []"
    :total="stock.data.value?.totalCount ?? 0"
    :loading="stock.isFetching.value"
    :error="stock.error.value"
    :has-filters="table.hasFilters"
    :empty-title="t('admin.stock.empty')"
    @retry="stock.refetch()"
    @clear-filters="table.clearFilters()"
  >
    <template #filters>
      <el-select
        v-model="table.filters.warehouseId"
        class="!w-full sm:!w-48"
        clearable
        value-on-clear=""
        :empty-values="['', null, undefined]"
        :placeholder="t('admin.stock.allWarehouses')"
        :aria-label="t('admin.stock.warehouse')"
        data-testid="filter-warehouse"
      >
        <el-option
          v-for="warehouse in warehouses.data.value"
          :key="warehouse.id"
          :value="warehouse.id"
          :label="warehouse.name"
        />
      </el-select>
      <el-checkbox v-model="lowOnly" border data-testid="filter-low">
        {{ t('admin.stock.lowOnly') }}
      </el-checkbox>
      <el-tag v-if="table.filters.variantId" type="info" disable-transitions>
        {{ t('admin.stock.oneVariant') }}
      </el-tag>
    </template>
    <template #cell-productName="{ row }">
      <span class="font-semibold text-ink" data-testid="stock-row">{{ row.productName }}</span>
      <el-tag v-if="row.isLow" type="warning" size="small" class="ml-2" disable-transitions>
        {{ t('admin.stock.low') }}
      </el-tag>
    </template>
    <template #cell-actions="{ row }">
      <RouterLink
        class="mr-2 text-sm font-semibold text-primary"
        :to="{
          name: 'movements',
          query: { variantId: row.variantId, warehouseId: row.warehouseId },
        }"
      >
        {{ t('admin.stock.history') }}
      </RouterLink>
      <el-button
        v-if="canWrite"
        size="small"
        data-testid="stock-threshold"
        @click="threshold.show(row)"
      >
        {{ t('admin.stock.threshold') }}
      </el-button>
      <el-button v-if="canWrite" size="small" data-testid="stock-adjust" @click="adjust.show(row)">
        {{ t('admin.stock.adjustShort') }}
      </el-button>
    </template>
  </DataTable>

  <AdjustStockForm
    v-if="adjust.mounted.value"
    :key="adjust.session.value"
    v-model="adjust.open.value"
    :stock="adjust.item.value"
    @closed="adjust.closed()"
  />
  <ThresholdForm
    v-if="threshold.mounted.value && threshold.item.value"
    :key="threshold.session.value"
    v-model="threshold.open.value"
    :stock="threshold.item.value"
    @closed="threshold.closed()"
  />
</template>
