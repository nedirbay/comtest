<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi, useAuthStore } from '@comtest/auth'
import { AppButton, PageHeader, StatusTag, useApiErrorMessage } from '@comtest/ui'
import { Plus } from '@element-plus/icons-vue'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/vue-query'
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'
import { Permission } from '@/app/permissions'
import { runBulk } from '@/shared/bulk'
import DataTable, { type DataColumn } from '@/shared/components/DataTable.vue'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useFormat } from '@/shared/composables/useFormat'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { toastSuccess, toastWarning } from '@/shared/toast'
import { PRODUCTS_KEY } from '../queries'

type Row = Schema<'ProductAdminListItemDto'>
type ProductStatus = Schema<'ProductStatus'>

const PRODUCT_STATUSES: ProductStatus[] = ['draft', 'published', 'archived']

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const format = useFormat()
const confirm = useConfirm()
const queryClient = useQueryClient()
const describeError = useApiErrorMessage()

const table = useTableQuery({ filters: { status: '' }, defaultSort: '-createdAt' })
const canWrite = computed(() => auth.can(Permission.CatalogWrite))
const tableRef = ref<{ clearSelection: () => void } | null>(null)
const selected = ref<Row[]>([])

const products = useQuery({
  queryKey: [
    ...PRODUCTS_KEY,
    'list',
    computed(() => ({
      page: table.page,
      pageSize: table.pageSize,
      sort: table.sort,
      status: table.filters.status,
    })),
  ],
  placeholderData: keepPreviousData,
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/catalog/products', {
        params: {
          query: {
            page: table.page,
            pageSize: table.pageSize,
            sort: table.sort || undefined,
            status: (table.filters.status || undefined) as ProductStatus | undefined,
          },
        },
      }),
    ),
})

const columns = computed<DataColumn[]>(() => [
  { key: 'name', label: t('admin.products.name'), minWidth: 220, required: true },
  { key: 'slug', label: t('admin.products.slug'), minWidth: 180, sortable: true },
  { key: 'status', label: t('admin.products.status'), minWidth: 130, sortable: true },
  { key: 'variantCount', label: t('admin.products.variantCount'), minWidth: 110, align: 'right' },
  { key: 'createdAt', label: t('admin.products.createdAt'), minWidth: 190, sortable: true },
])

/** Reports the outcome. If not one item went through, the dialog stays and shows why. */
async function finishBulk(run: ReturnType<typeof runBulk>): Promise<void> {
  const result = await run

  await queryClient.invalidateQueries({ queryKey: PRODUCTS_KEY })
  tableRef.value?.clearSelection()
  selected.value = []

  if (result.succeeded === 0) {
    throw result.firstError
  }

  if (result.failed > 0) {
    toastWarning(
      t('admin.products.bulkPartial', {
        succeeded: result.succeeded,
        failed: result.failed,
        reason: describeError(result.firstError),
      }),
    )
  } else {
    toastSuccess(t('admin.products.bulkDone', { count: result.succeeded }))
  }
}

function changeStatus(status: 'published' | 'archived'): void {
  const rows = [...selected.value]

  confirm.ask({
    title: t(status === 'published' ? 'admin.products.bulkPublish' : 'admin.products.bulkArchive'),
    message: t(
      status === 'published'
        ? 'admin.products.bulkPublishConfirm'
        : 'admin.products.bulkArchiveConfirm',
      { count: rows.length },
    ),
    action: () =>
      finishBulk(
        runBulk(rows, (row) =>
          unwrap(
            api.client.PUT('/api/v1/admin/catalog/products/{id}/status', {
              params: { path: { id: row.id } },
              body: { status },
            }),
          ),
        ),
      ),
  })
}

function deleteSelected(): void {
  const rows = [...selected.value]

  confirm.ask({
    title: t('admin.products.bulkDelete'),
    message: t('admin.products.bulkDeleteConfirm', { count: rows.length }),
    confirmText: t('common.delete'),
    danger: true,
    action: () =>
      finishBulk(
        runBulk(rows, (row) =>
          unwrap(
            api.client.DELETE('/api/v1/admin/catalog/products/{id}', {
              params: { path: { id: row.id } },
            }),
          ),
        ),
      ),
  })
}
</script>

<template>
  <PageHeader :title="t('admin.nav.products')" :description="t('admin.products.intro')">
    <template #actions>
      <RouterLink v-if="canWrite" :to="{ name: 'product-new' }">
        <el-button type="primary" :icon="Plus" data-testid="new-product">
          {{ t('admin.products.new') }}
        </el-button>
      </RouterLink>
    </template>
  </PageHeader>

  <div
    v-if="selected.length"
    class="mb-3 flex flex-wrap items-center gap-2 rounded-card border border-primary bg-primary-soft p-3"
    data-testid="bulk-bar"
  >
    <span class="mr-auto text-sm font-semibold text-ink">
      {{ t('admin.products.selectedCount', { count: selected.length }) }}
    </span>
    <AppButton data-testid="bulk-publish" @click="changeStatus('published')">
      {{ t('admin.products.bulkPublish') }}
    </AppButton>
    <AppButton data-testid="bulk-archive" @click="changeStatus('archived')">
      {{ t('admin.products.bulkArchive') }}
    </AppButton>
    <AppButton variant="danger" data-testid="bulk-delete" @click="deleteSelected">
      {{ t('admin.products.bulkDelete') }}
    </AppButton>
  </div>

  <DataTable
    id="products"
    ref="tableRef"
    v-model:page="table.page"
    v-model:page-size="table.pageSize"
    v-model:sort="table.sort"
    :columns="columns"
    :rows="products.data.value?.items ?? []"
    :total="products.data.value?.totalCount ?? 0"
    :loading="products.isFetching.value"
    :error="products.error.value"
    :selectable="canWrite"
    :has-filters="table.hasFilters"
    :empty-title="t('admin.products.empty')"
    @retry="products.refetch()"
    @clear-filters="table.clearFilters()"
    @selection-change="selected = $event"
  >
    <template #filters>
      <el-select
        v-model="table.filters.status"
        class="!w-full sm:!w-44"
        clearable
        :placeholder="t('admin.products.allStatuses')"
        :aria-label="t('admin.products.status')"
        data-testid="filter-status"
        :empty-values="['', null, undefined]"
        value-on-clear=""
      >
        <el-option
          v-for="status in PRODUCT_STATUSES"
          :key="status"
          :value="status"
          :label="t(`enums.productStatus.${status}`)"
        />
      </el-select>
    </template>
    <template #cell-name="{ row }">
      <RouterLink
        :to="{ name: 'product-edit', params: { id: row.id } }"
        class="font-semibold text-primary"
        data-testid="product-link"
      >
        {{ row.name ?? row.slug }}
      </RouterLink>
    </template>
    <template #cell-status="{ row }">
      <StatusTag kind="productStatus" :value="row.status" />
    </template>
    <template #cell-createdAt="{ row }">{{ format.dateTime(row.createdAt) }}</template>
  </DataTable>
</template>
