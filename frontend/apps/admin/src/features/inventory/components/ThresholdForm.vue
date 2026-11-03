<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { AppField } from '@comtest/ui'
import { useQueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { z } from '@comtest/ui/zod'
import FormDrawer from '@/shared/components/FormDrawer.vue'
import { useAdminForm } from '@/shared/composables/useAdminForm'
import { toastSuccess } from '@/shared/toast'
import { STOCK_KEY } from '../queries'

const props = defineProps<{ stock: Schema<'StockItemDto'> }>()
const emit = defineEmits<{ closed: [] }>()
const open = defineModel<boolean>({ required: true })

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const queryClient = useQueryClient()

const form = useAdminForm({
  schema: z.object({ threshold: z.number().int().min(0).max(1_000_000) }),
  initial: { threshold: props.stock.lowStockThreshold },
  submit: async (values) => {
    await unwrap(
      api.client.PUT('/api/v1/admin/inventory/stock/{id}/threshold', {
        params: { path: { id: props.stock.id } },
        body: values,
      }),
    )

    await queryClient.invalidateQueries({ queryKey: STOCK_KEY })
    toastSuccess(t('admin.stock.thresholdSaved'))
    open.value = false
  },
})
</script>

<template>
  <FormDrawer
    v-model="open"
    :title="t('admin.stock.threshold')"
    :error="form.formError.value"
    :submitting="form.submitting.value"
    @submit="form.handleSubmit()"
    @closed="emit('closed')"
  >
    <p class="mb-3 text-sm text-ink">
      <span class="font-semibold">{{ stock.productName }}</span>
      <span class="text-ink-muted">· {{ stock.sku }} · {{ stock.warehouseCode }}</span>
    </p>
    <AppField
      name="threshold"
      :label="t('admin.stock.threshold')"
      :hint="t('admin.stock.thresholdHint')"
    >
      <el-input-number
        v-model="form.values.threshold"
        :min="0"
        :max="1000000"
        data-testid="threshold-input"
      />
    </AppField>
  </FormDrawer>
</template>
