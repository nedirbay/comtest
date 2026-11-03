<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi, useAuthStore } from '@comtest/auth'
import { AppTable, PageHeader, type TableColumn } from '@comtest/ui'
import { Delete, Edit, Plus } from '@element-plus/icons-vue'
import { useQueryClient } from '@tanstack/vue-query'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { Permission } from '@/app/permissions'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useDrawerItem } from '@/shared/composables/useDrawerItem'
import { toastSuccess } from '@/shared/toast'
import WarehouseForm from '../components/WarehouseForm.vue'
import { WAREHOUSES_KEY, useWarehouses } from '../queries'

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const queryClient = useQueryClient()
const confirm = useConfirm()

const warehouses = useWarehouses()
const canWrite = computed(() => auth.can(Permission.InventoryWrite))
const drawer = useDrawerItem<Schema<'WarehouseDto'>>()

const columns = computed<TableColumn[]>(() => [
  { key: 'name', label: t('admin.common.name'), minWidth: 180 },
  { key: 'code', label: t('admin.warehouses.code'), minWidth: 110 },
  { key: 'address', label: t('admin.warehouses.address'), minWidth: 200 },
  { key: 'flags', label: t('admin.warehouses.state'), minWidth: 170 },
  { key: 'actions', label: '', width: 110, align: 'right' },
])

function remove(warehouse: Schema<'WarehouseDto'>): void {
  confirm.ask({
    title: t('admin.warehouses.delete'),
    message: t('admin.warehouses.deleteConfirm', { name: warehouse.name }),
    confirmText: t('common.delete'),
    danger: true,
    action: async () => {
      await unwrap(
        api.client.DELETE('/api/v1/admin/inventory/warehouses/{id}', {
          params: { path: { id: warehouse.id } },
        }),
      )
      await queryClient.invalidateQueries({ queryKey: WAREHOUSES_KEY })
      toastSuccess(t('admin.warehouses.deleted'))
    },
  })
}
</script>

<template>
  <PageHeader :title="t('admin.nav.warehouses')" :description="t('admin.warehouses.intro')">
    <template #actions>
      <el-button
        v-if="canWrite"
        type="primary"
        :icon="Plus"
        data-testid="new-warehouse"
        @click="drawer.show()"
      >
        {{ t('admin.warehouses.new') }}
      </el-button>
    </template>
  </PageHeader>

  <AppTable
    :columns="columns"
    :rows="warehouses.data.value ?? []"
    :loading="warehouses.isPending.value"
    :error="warehouses.error.value"
    :empty-title="t('admin.warehouses.empty')"
    @retry="warehouses.refetch()"
  >
    <template #cell-name="{ row }">
      <span class="font-semibold text-ink" data-testid="warehouse-row">{{ row.name }}</span>
    </template>
    <template #cell-flags="{ row }">
      <span class="flex flex-wrap gap-1">
        <el-tag v-if="row.isDefault" type="success" disable-transitions>
          {{ t('admin.warehouses.isDefault') }}
        </el-tag>
        <el-tag :type="row.isActive ? 'primary' : 'info'" disable-transitions>
          {{ t(row.isActive ? 'admin.common.active' : 'admin.common.inactive') }}
        </el-tag>
      </span>
    </template>
    <template #cell-actions="{ row }">
      <el-button
        v-if="canWrite"
        :icon="Edit"
        text
        size="small"
        :aria-label="t('admin.common.edit')"
        data-testid="warehouse-edit"
        @click="drawer.show(row)"
      />
      <el-button
        v-if="canWrite"
        :icon="Delete"
        text
        size="small"
        type="danger"
        :aria-label="t('common.delete')"
        data-testid="warehouse-delete"
        @click="remove(row)"
      />
    </template>
  </AppTable>

  <WarehouseForm
    v-if="drawer.mounted.value"
    :key="drawer.session.value"
    v-model="drawer.open.value"
    :warehouse="drawer.item.value"
    :readonly="!canWrite"
    @closed="drawer.closed()"
  />
</template>
