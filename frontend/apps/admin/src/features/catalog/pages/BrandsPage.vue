<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi, useAuthStore } from '@comtest/auth'
import { AppTable, PageHeader, safeUrl, type TableColumn } from '@comtest/ui'
import { Delete, Edit, Plus } from '@element-plus/icons-vue'
import { useQueryClient } from '@tanstack/vue-query'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { Permission } from '@/app/permissions'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useDrawerItem } from '@/shared/composables/useDrawerItem'
import { toastSuccess } from '@/shared/toast'
import BrandForm from '../components/BrandForm.vue'
import { BRANDS_KEY, useBrands } from '../queries'

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const queryClient = useQueryClient()
const confirm = useConfirm()

const brands = useBrands()
const canWrite = computed(() => auth.can(Permission.CatalogWrite))
const drawer = useDrawerItem<Schema<'BrandDto'>>()

const columns = computed<TableColumn[]>(() => [
  { key: 'name', label: t('admin.common.name'), minWidth: 200 },
  { key: 'slug', label: t('admin.products.slug'), minWidth: 180 },
  { key: 'logoUrl', label: t('admin.brands.logo'), width: 90 },
  { key: 'actions', label: '', width: 110, align: 'right' },
])

function remove(brand: Schema<'BrandDto'>): void {
  confirm.ask({
    title: t('admin.brands.delete'),
    message: t('admin.brands.deleteConfirm', { name: brand.name }),
    confirmText: t('common.delete'),
    danger: true,
    action: async () => {
      await unwrap(
        api.client.DELETE('/api/v1/admin/catalog/brands/{id}', {
          params: { path: { id: brand.id } },
        }),
      )
      await queryClient.invalidateQueries({ queryKey: BRANDS_KEY })
      toastSuccess(t('admin.brands.deleted'))
    },
  })
}
</script>

<template>
  <PageHeader :title="t('admin.nav.brands')" :description="t('admin.brands.intro')">
    <template #actions>
      <el-button
        v-if="canWrite"
        type="primary"
        :icon="Plus"
        data-testid="new-brand"
        @click="drawer.show()"
      >
        {{ t('admin.brands.new') }}
      </el-button>
    </template>
  </PageHeader>

  <AppTable
    :columns="columns"
    :rows="brands.data.value ?? []"
    :loading="brands.isPending.value"
    :error="brands.error.value"
    :empty-title="t('admin.brands.empty')"
    @retry="brands.refetch()"
  >
    <template #cell-name="{ row }">
      <span class="font-semibold text-ink" data-testid="brand-row">{{ row.name }}</span>
    </template>
    <template #cell-logoUrl="{ row }">
      <img
        v-if="safeUrl(row.logoUrl)"
        :src="safeUrl(row.logoUrl)"
        alt=""
        class="h-8 max-w-16 object-contain"
      />
    </template>
    <template #cell-actions="{ row }">
      <el-button
        :icon="Edit"
        text
        size="small"
        :aria-label="t('admin.common.edit')"
        data-testid="brand-edit"
        @click="drawer.show(row)"
      />
      <el-button
        v-if="canWrite"
        :icon="Delete"
        text
        size="small"
        type="danger"
        :aria-label="t('common.delete')"
        data-testid="brand-delete"
        @click="remove(row)"
      />
    </template>
  </AppTable>

  <BrandForm
    v-if="drawer.mounted.value"
    :key="drawer.session.value"
    v-model="drawer.open.value"
    :brand="drawer.item.value"
    :readonly="!canWrite"
    @closed="drawer.closed()"
  />
</template>
