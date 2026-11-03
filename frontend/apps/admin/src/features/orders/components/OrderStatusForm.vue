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
import { toastSuccess } from '@/shared/toast'
import { nullIfEmpty, optionalText, optionalUrl } from '@/shared/validation'
import { isUndoingStatus, ORDERS_KEY } from '../queries'

const props = defineProps<{ orderId: string; orderNumber: string; status: Schema<'OrderStatus'> }>()
const emit = defineEmits<{ closed: [] }>()
const open = defineModel<boolean>({ required: true })

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const queryClient = useQueryClient()
const confirm = useConfirm()

const isShipping = computed(() => props.status === 'shipped')
const undoing = computed(() => isUndoingStatus(props.status))
const statusLabel = computed(() => t(`enums.orderStatus.${props.status}`))

const form = useAdminForm({
  schema: z.object({
    note: optionalText(1000),
    carrier: optionalText(100),
    trackingNumber: optionalText(100),
    trackingUrl: optionalUrl,
  }),
  initial: { note: '', carrier: '', trackingNumber: '', trackingUrl: '' },
  submit: async (values) => {
    const send = async () => {
      const saved = await unwrap(
        api.client.PUT('/api/v1/admin/orders/{id}/status', {
          params: { path: { id: props.orderId } },
          body: {
            status: props.status,
            note: nullIfEmpty(values.note),
            ...(isShipping.value
              ? {
                  carrier: nullIfEmpty(values.carrier),
                  trackingNumber: nullIfEmpty(values.trackingNumber),
                  trackingUrl: nullIfEmpty(values.trackingUrl),
                }
              : {}),
          },
        }),
      )

      queryClient.setQueryData([...ORDERS_KEY, 'detail', props.orderId], saved)
      await queryClient.invalidateQueries({ queryKey: ORDERS_KEY })
      toastSuccess(t('admin.order.statusChanged', { status: statusLabel.value }))
      open.value = false
    }

    if (undoing.value) {
      confirm.ask({
        title: t(props.status === 'cancelled' ? 'admin.order.cancel' : 'admin.order.return'),
        message: t(
          props.status === 'cancelled' ? 'admin.order.cancelConfirm' : 'admin.order.returnConfirm',
          { number: props.orderNumber },
        ),
        confirmText: t(props.status === 'cancelled' ? 'admin.order.cancel' : 'admin.order.return'),
        danger: true,
        action: send,
      })

      return
    }

    await send()
  },
})
</script>

<template>
  <FormDrawer
    v-model="open"
    :title="t('admin.order.changeStatusTo', { status: statusLabel })"
    :error="form.formError.value"
    :submitting="form.submitting.value"
    :submit-text="statusLabel"
    @submit="form.handleSubmit()"
    @closed="emit('closed')"
  >
    <template v-if="isShipping">
      <AppField name="carrier" :label="t('admin.order.carrier')">
        <el-input v-model="form.values.carrier" maxlength="100" data-testid="status-carrier" />
      </AppField>
      <AppField name="trackingNumber" :label="t('admin.order.trackingNumber')">
        <el-input
          v-model="form.values.trackingNumber"
          maxlength="100"
          data-testid="status-tracking-number"
        />
      </AppField>
      <AppField name="trackingUrl" :label="t('admin.order.trackingUrl')">
        <el-input
          v-model="form.values.trackingUrl"
          maxlength="500"
          inputmode="url"
          data-testid="status-tracking-url"
        />
      </AppField>
    </template>
    <AppField name="note" :label="t('admin.order.statusNote')">
      <el-input
        v-model="form.values.note"
        type="textarea"
        :rows="3"
        maxlength="1000"
        data-testid="status-note"
      />
    </AppField>
  </FormDrawer>
</template>
