<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { AppField } from '@comtest/ui'
import { useQueryClient } from '@tanstack/vue-query'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { z } from '@comtest/ui/zod'
import FormDrawer from '@/shared/components/FormDrawer.vue'
import { useAdminForm } from '@/shared/composables/useAdminForm'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useFormat } from '@/shared/composables/useFormat'
import { toastSuccess } from '@/shared/toast'
import { requiredAmount, requiredText } from '@/shared/validation'
import { ORDERS_KEY } from '../queries'

const props = defineProps<{ orderId: string; payment: Schema<'PaymentDto'> }>()
const emit = defineEmits<{ closed: [] }>()
const open = defineModel<boolean>({ required: true })

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const queryClient = useQueryClient()
const confirm = useConfirm()
const format = useFormat()

const currency = computed(() => props.payment.currency)

const form = useAdminForm({
  schema: z.object({ amount: requiredAmount, reason: requiredText(1000) }),
  initial: { amount: '', reason: '' },
  submit: async (values) => {
    // What may still be refunded is checked by the server, which also holds the money figures.
    confirm.ask({
      title: t('admin.order.requestRefund'),
      message: t('admin.order.refundConfirm', {
        amount: format.money(Number(values.amount), currency.value),
      }),
      confirmText: t('admin.order.requestRefund'),
      danger: true,
      action: async () => {
        await unwrap(
          api.client.POST('/api/v1/admin/orders/{id}/payment/refunds', {
            params: { path: { id: props.orderId } },
            body: { amount: Number(values.amount), reason: values.reason },
          }),
        )

        await queryClient.invalidateQueries({ queryKey: ORDERS_KEY })
        toastSuccess(t('admin.order.refundRequested'))
        open.value = false
      },
    })
  },
})
</script>

<template>
  <FormDrawer
    v-model="open"
    :title="t('admin.order.requestRefund')"
    :error="form.formError.value"
    :submitting="form.submitting.value"
    :submit-text="t('admin.order.requestRefund')"
    @submit="form.handleSubmit()"
    @closed="emit('closed')"
  >
    <dl class="mb-4 grid grid-cols-2 gap-2 text-sm">
      <dt class="text-ink-muted">{{ t('admin.order.paidAmount') }}</dt>
      <dd class="text-right font-semibold tabular-nums text-ink">
        {{ format.money(payment.amount, payment.currency) }}
      </dd>
      <dt class="text-ink-muted">{{ t('admin.order.refundedAmount') }}</dt>
      <dd class="text-right tabular-nums text-ink">
        {{ format.money(payment.refundedAmount, payment.currency) }}
      </dd>
      <dt class="text-ink-muted">{{ t('admin.order.pendingRefund') }}</dt>
      <dd class="text-right tabular-nums text-ink">
        {{ format.money(payment.pendingRefundAmount, payment.currency) }}
      </dd>
    </dl>
    <AppField name="amount" :label="t('admin.order.refundAmount', { currency })">
      <el-input v-model="form.values.amount" inputmode="decimal" data-testid="refund-amount" />
    </AppField>
    <AppField name="reason" :label="t('admin.order.refundReason')">
      <el-input
        v-model="form.values.reason"
        type="textarea"
        :rows="3"
        maxlength="1000"
        data-testid="refund-reason"
      />
    </AppField>
  </FormDrawer>
</template>
