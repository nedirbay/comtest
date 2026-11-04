<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { PageHeader, StatusTag } from '@comtest/ui'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/vue-query'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import DataTable, { type DataColumn } from '@/shared/components/DataTable.vue'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useTableQuery } from '@/shared/composables/useTableQuery'
import { toastSuccess } from '@/shared/toast'

type Tenant = Schema<'TenantDto'>

const TENANTS_KEY = ['platform', 'tenants']
const STATUSES: Schema<'TenantStatus'>[] = ['active', 'suspended']

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const queryClient = useQueryClient()
const confirm = useConfirm()

const table = useTableQuery({ filters: { status: '' } })

const tenants = useQuery({
  queryKey: [
    ...TENANTS_KEY,
    'list',
    computed(() => ({
      page: table.page,
      pageSize: table.pageSize,
      status: table.filters.status,
    })),
  ],
  placeholderData: keepPreviousData,
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/platform/tenants', {
        params: {
          query: {
            page: table.page,
            pageSize: table.pageSize,
            status: (table.filters.status || undefined) as Schema<'TenantStatus'> | undefined,
          },
        },
      }),
    ),
})

const columns = computed<DataColumn[]>(() => [
  { key: 'name', label: t('admin.platform.tenant'), minWidth: 200, required: true },
  { key: 'slug', label: t('admin.platform.slug'), minWidth: 170 },
  { key: 'plan', label: t('admin.settings.plan'), minWidth: 110 },
  { key: 'status', label: t('admin.orders.status'), minWidth: 130 },
  { key: 'actions', label: '', minWidth: 140, align: 'right', required: true },
])

function setStatus(tenant: Tenant, status: Schema<'TenantStatus'>): void {
  const suspending = status === 'suspended'

  confirm.ask({
    title: t(suspending ? 'admin.platform.suspend' : 'admin.platform.activate'),
    message: t(suspending ? 'admin.platform.suspendConfirm' : 'admin.platform.activateConfirm', {
      name: tenant.name,
    }),
    confirmText: t(suspending ? 'admin.platform.suspend' : 'admin.platform.activate'),
    danger: suspending,
    action: async () => {
      await unwrap(
        api.client.PUT('/api/v1/platform/tenants/{id}/status', {
          params: { path: { id: tenant.id } },
          body: { status },
        }),
      )
      await queryClient.invalidateQueries({ queryKey: TENANTS_KEY })
      toastSuccess(t('admin.platform.changed'))
    },
  })
}
</script>

<template>
  <PageHeader :title="t('admin.nav.tenants')" :description="t('admin.platform.intro')" />

  <DataTable
    id="tenants"
    v-model:page="table.page"
    v-model:page-size="table.pageSize"
    :columns="columns"
    :rows="tenants.data.value?.items ?? []"
    :total="tenants.data.value?.totalCount ?? 0"
    :loading="tenants.isFetching.value"
    :error="tenants.error.value"
    :has-filters="table.hasFilters"
    :empty-title="t('admin.platform.empty')"
    @retry="tenants.refetch()"
    @clear-filters="table.clearFilters()"
  >
    <template #filters>
      <el-select
        v-model="table.filters.status"
        class="!w-full sm:!w-44"
        clearable
        value-on-clear=""
        :empty-values="['', null, undefined]"
        :placeholder="t('admin.products.allStatuses')"
        :aria-label="t('admin.orders.status')"
        data-testid="filter-status"
      >
        <el-option
          v-for="status in STATUSES"
          :key="status"
          :value="status"
          :label="t(`enums.tenantStatus.${status}`)"
        />
      </el-select>
    </template>
    <template #cell-name="{ row }">
      <span class="font-semibold text-ink" data-testid="tenant-row">{{ row.name }}</span>
    </template>
    <template #cell-status="{ row }">
      <StatusTag kind="tenantStatus" :value="row.status" />
    </template>
    <template #cell-actions="{ row }">
      <el-button
        v-if="row.status === 'active'"
        size="small"
        type="danger"
        plain
        data-testid="tenant-suspend"
        @click="setStatus(row, 'suspended')"
      >
        {{ t('admin.platform.suspend') }}
      </el-button>
      <el-button
        v-else
        size="small"
        data-testid="tenant-activate"
        @click="setStatus(row, 'active')"
      >
        {{ t('admin.platform.activate') }}
      </el-button>
    </template>
  </DataTable>
</template>
