<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { AppTable, type TableColumn } from '@comtest/ui'
import { Delete, Edit, Plus } from '@element-plus/icons-vue'
import { useQuery, useQueryClient } from '@tanstack/vue-query'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useDrawerItem } from '@/shared/composables/useDrawerItem'
import { useFormat } from '@/shared/composables/useFormat'
import { toastSuccess } from '@/shared/toast'
import MethodForm from './MethodForm.vue'
import ZoneForm from './ZoneForm.vue'

const { t, te, locale } = useI18n({ useScope: 'global' })
const api = useApi()
const queryClient = useQueryClient()
const confirm = useConfirm()
const format = useFormat()

const zones = useQuery({
  queryKey: ['admin', 'shipping', 'zones'],
  queryFn: () => unwrap(api.client.GET('/api/v1/admin/shipping/zones')),
})

const methods = useQuery({
  queryKey: ['admin', 'shipping', 'methods'],
  queryFn: () => unwrap(api.client.GET('/api/v1/admin/shipping/methods')),
})

const zoneDrawer = useDrawerItem<Schema<'ShippingZoneDto'>>()
const methodDrawer = useDrawerItem<Schema<'ShippingMethodDto'>>()

const zoneColumns = computed<TableColumn[]>(() => [
  { key: 'name', label: t('admin.common.name'), minWidth: 160 },
  { key: 'regions', label: t('admin.shipping.regions'), minWidth: 240 },
  { key: 'isActive', label: t('admin.warehouses.state'), minWidth: 110 },
  { key: 'actions', label: '', width: 110, align: 'right' },
])

const methodColumns = computed<TableColumn[]>(() => [
  { key: 'name', label: t('admin.common.name'), minWidth: 180 },
  { key: 'type', label: t('admin.shipping.type'), minWidth: 110 },
  { key: 'zone', label: t('admin.shipping.zone'), minWidth: 140 },
  { key: 'price', label: t('admin.productForm.price'), minWidth: 110, align: 'right' },
  { key: 'isActive', label: t('admin.warehouses.state'), minWidth: 110 },
  { key: 'actions', label: '', width: 110, align: 'right' },
])

const regionLabel = (code: string) => (te(`regions.${code}`) ? t(`regions.${code}`) : code)

function methodName(method: Schema<'ShippingMethodDto'>): string {
  return method.names[locale.value] ?? Object.values(method.names)[0] ?? ''
}

const zoneName = (id: string | null) =>
  zones.data.value?.find((zone) => zone.id === id)?.name ?? '—'

function removeZone(zone: Schema<'ShippingZoneDto'>): void {
  confirm.ask({
    title: t('admin.shipping.zoneDelete'),
    message: t('admin.shipping.zoneDeleteConfirm', { name: zone.name }),
    confirmText: t('common.delete'),
    danger: true,
    action: async () => {
      await unwrap(
        api.client.DELETE('/api/v1/admin/shipping/zones/{id}', {
          params: { path: { id: zone.id } },
        }),
      )
      await queryClient.invalidateQueries({ queryKey: ['admin', 'shipping'] })
      toastSuccess(t('admin.shipping.deleted'))
    },
  })
}

function removeMethod(method: Schema<'ShippingMethodDto'>): void {
  confirm.ask({
    title: t('admin.shipping.methodDelete'),
    message: t('admin.shipping.methodDeleteConfirm', { name: methodName(method) }),
    confirmText: t('common.delete'),
    danger: true,
    action: async () => {
      await unwrap(
        api.client.DELETE('/api/v1/admin/shipping/methods/{id}', {
          params: { path: { id: method.id } },
        }),
      )
      await queryClient.invalidateQueries({ queryKey: ['admin', 'shipping'] })
      toastSuccess(t('admin.shipping.deleted'))
    },
  })
}
</script>

<template>
  <div class="space-y-6">
    <section>
      <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
        <h2 class="font-bold text-ink">{{ t('admin.shipping.zones') }}</h2>
        <el-button :icon="Plus" data-testid="new-zone" @click="zoneDrawer.show()">
          {{ t('admin.shipping.zoneNew') }}
        </el-button>
      </div>
      <AppTable
        :columns="zoneColumns"
        :rows="zones.data.value ?? []"
        :loading="zones.isPending.value"
        :error="zones.error.value"
        :empty-title="t('admin.shipping.noZones')"
        @retry="zones.refetch()"
      >
        <template #cell-name="{ row }">
          <span class="font-semibold text-ink" data-testid="zone-row">{{ row.name }}</span>
        </template>
        <template #cell-regions="{ row }">{{ row.regions.map(regionLabel).join(', ') }}</template>
        <template #cell-isActive="{ row }">
          <el-tag :type="row.isActive ? 'primary' : 'info'" disable-transitions>
            {{ t(row.isActive ? 'admin.common.active' : 'admin.common.inactive') }}
          </el-tag>
        </template>
        <template #cell-actions="{ row }">
          <el-button
            :icon="Edit"
            text
            size="small"
            :aria-label="t('admin.common.edit')"
            @click="zoneDrawer.show(row)"
          />
          <el-button
            :icon="Delete"
            text
            size="small"
            type="danger"
            :aria-label="t('common.delete')"
            data-testid="zone-delete"
            @click="removeZone(row)"
          />
        </template>
      </AppTable>
    </section>

    <section>
      <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
        <h2 class="font-bold text-ink">{{ t('admin.shipping.methods') }}</h2>
        <el-button :icon="Plus" data-testid="new-method" @click="methodDrawer.show()">
          {{ t('admin.shipping.methodNew') }}
        </el-button>
      </div>
      <AppTable
        :columns="methodColumns"
        :rows="methods.data.value ?? []"
        :loading="methods.isPending.value"
        :error="methods.error.value"
        :empty-title="t('admin.shipping.noMethods')"
        @retry="methods.refetch()"
      >
        <template #cell-name="{ row }">
          <span class="font-semibold text-ink" data-testid="method-row">{{ methodName(row) }}</span>
        </template>
        <template #cell-type="{ row }">
          {{ t(`enums.shippingType.${row.type}`) }}
        </template>
        <template #cell-zone="{ row }">{{ zoneName(row.zoneId) }}</template>
        <template #cell-price="{ row }">{{ format.money(row.price, row.currency) }}</template>
        <template #cell-isActive="{ row }">
          <el-tag :type="row.isActive ? 'primary' : 'info'" disable-transitions>
            {{ t(row.isActive ? 'admin.common.active' : 'admin.common.inactive') }}
          </el-tag>
        </template>
        <template #cell-actions="{ row }">
          <el-button
            :icon="Edit"
            text
            size="small"
            :aria-label="t('admin.common.edit')"
            @click="methodDrawer.show(row)"
          />
          <el-button
            :icon="Delete"
            text
            size="small"
            type="danger"
            :aria-label="t('common.delete')"
            data-testid="method-delete"
            @click="removeMethod(row)"
          />
        </template>
      </AppTable>
    </section>

    <ZoneForm
      v-if="zoneDrawer.mounted.value"
      :key="zoneDrawer.session.value"
      v-model="zoneDrawer.open.value"
      :zone="zoneDrawer.item.value"
      @closed="zoneDrawer.closed()"
    />
    <MethodForm
      v-if="methodDrawer.mounted.value"
      :key="methodDrawer.session.value"
      v-model="methodDrawer.open.value"
      :method="methodDrawer.item.value"
      :zones="zones.data.value ?? []"
      @closed="methodDrawer.closed()"
    />
  </div>
</template>
