<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { AppTable, type TableColumn } from '@comtest/ui'
import { useQuery } from '@tanstack/vue-query'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useFormat } from '@/shared/composables/useFormat'
import { PRODUCTS_KEY } from '../queries'

const props = defineProps<{ productId: string; variants: Schema<'VariantAdminDto'>[] }>()

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const format = useFormat()

const history = useQuery({
  queryKey: [...PRODUCTS_KEY, 'detail', computed(() => props.productId), 'price-history'],
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/catalog/products/{id}/price-history', {
        params: { path: { id: props.productId } },
      }),
    ),
})

const skuOf = (variantId: string) =>
  props.variants.find((variant) => variant.id === variantId)?.sku ?? variantId.slice(0, 8)

const columns = computed<TableColumn[]>(() => [
  { key: 'sku', label: t('admin.productForm.sku'), minWidth: 140 },
  { key: 'oldAmount', label: t('admin.productForm.oldAmount'), minWidth: 120, align: 'right' },
  { key: 'newAmount', label: t('admin.productForm.newAmount'), minWidth: 120, align: 'right' },
  { key: 'changedAt', label: t('admin.productForm.changedAt'), minWidth: 190 },
])

const rows = computed(() =>
  (history.data.value ?? []).map((change, index) => ({ ...change, id: index })),
)
</script>

<template>
  <AppTable
    :columns="columns"
    :rows="rows"
    :loading="history.isPending.value"
    :error="history.error.value"
    :empty-title="t('admin.productForm.noPriceChanges')"
    @retry="history.refetch()"
  >
    <template #cell-sku="{ row }">{{ skuOf(row.variantId) }}</template>
    <template #cell-oldAmount="{ row }">{{ format.money(row.oldAmount, row.currency) }}</template>
    <template #cell-newAmount="{ row }">{{ format.money(row.newAmount, row.currency) }}</template>
    <template #cell-changedAt="{ row }">{{ format.dateTime(row.changedAt) }}</template>
  </AppTable>
</template>
