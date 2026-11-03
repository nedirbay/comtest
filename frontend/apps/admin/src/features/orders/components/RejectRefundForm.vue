<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { AppField } from '@comtest/ui'
import { useQueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { z } from '@comtest/ui/zod'
import FormDrawer from '@/shared/components/FormDrawer.vue'
import { useAdminForm } from '@/shared/composables/useAdminForm'
import { toastSuccess } from '@/shared/toast'
import { nullIfEmpty, optionalText } from '@/shared/validation'
import { ORDERS_KEY } from '../queries'

const props = defineProps<{ orderId: string; refundId: string }>()
const emit = defineEmits<{ closed: [] }>()
const open = defineModel<boolean>({ required: true })

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const queryClient = useQueryClient()

const form = useAdminForm({
  schema: z.object({ note: optionalText(1000) }),
  initial: { note: '' },
  submit: async (values) => {
    await unwrap(
      api.client.POST('/api/v1/admin/orders/{id}/payment/refunds/{refundId}/reject', {
        params: { path: { id: props.orderId, refundId: props.refundId } },
        body: { note: nullIfEmpty(values.note) },
      }),
    )

    await queryClient.invalidateQueries({ queryKey: ORDERS_KEY })
    toastSuccess(t('admin.order.refundRejected'))
    open.value = false
  },
})
</script>

<template>
  <FormDrawer
    v-model="open"
    :title="t('admin.order.rejectRefund')"
    :error="form.formError.value"
    :submitting="form.submitting.value"
    :submit-text="t('admin.order.rejectRefund')"
    @submit="form.handleSubmit()"
    @closed="emit('closed')"
  >
    <AppField name="note" :label="t('admin.order.statusNote')">
      <el-input
        v-model="form.values.note"
        type="textarea"
        :rows="3"
        maxlength="1000"
        data-testid="reject-note"
      />
    </AppField>
  </FormDrawer>
</template>
