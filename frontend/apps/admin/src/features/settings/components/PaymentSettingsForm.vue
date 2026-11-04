<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { AppButton, AppField, AppForm } from '@comtest/ui'
import { useI18n } from 'vue-i18n'
import { z } from '@comtest/ui/zod'
import { useAdminForm } from '@/shared/composables/useAdminForm'
import { toastSuccess } from '@/shared/toast'
import { nullIfEmpty, optionalAmount, optionalText, toAmount } from '@/shared/validation'

const props = defineProps<{ settings: Schema<'PaymentSettingsDto'> }>()
const emit = defineEmits<{ saved: [] }>()

const { t } = useI18n({ useScope: 'global' })
const api = useApi()

const form = useAdminForm({
  schema: z
    .object({
      acceptsCashOnDelivery: z.boolean(),
      acceptsBankTransfer: z.boolean(),
      bankTransferInstructions: optionalText(2000),
      refundApprovalThreshold: optionalAmount,
    })
    .superRefine((values, context) => {
      if (values.acceptsBankTransfer && !values.bankTransferInstructions) {
        context.addIssue({
          code: 'custom',
          path: ['bankTransferInstructions'],
          message: 'admin.settings.instructionsNeeded',
        })
      }
    }),
  initial: {
    acceptsCashOnDelivery: props.settings.acceptsCashOnDelivery,
    acceptsBankTransfer: props.settings.acceptsBankTransfer,
    bankTransferInstructions: props.settings.bankTransferInstructions ?? '',
    refundApprovalThreshold: props.settings.refundApprovalThreshold?.toString() ?? '',
  },
  submit: async (values) => {
    await unwrap(
      api.client.PUT('/api/v1/admin/store/payment-settings', {
        body: {
          acceptsCashOnDelivery: values.acceptsCashOnDelivery,
          acceptsBankTransfer: values.acceptsBankTransfer,
          bankTransferInstructions: nullIfEmpty(values.bankTransferInstructions),
          refundApprovalThreshold: toAmount(values.refundApprovalThreshold),
        },
      }),
    )
    emit('saved')
    toastSuccess(t('admin.settings.saved'))
  },
})
</script>

<template>
  <AppForm :error="form.formError.value" class="max-w-xl" @submit="form.handleSubmit()">
    <AppField name="acceptsCashOnDelivery">
      <el-switch
        v-model="form.values.acceptsCashOnDelivery"
        :active-text="t('enums.paymentMethod.cashOnDelivery')"
        :aria-label="t('enums.paymentMethod.cashOnDelivery')"
        data-testid="settings-cod"
      />
    </AppField>
    <AppField name="acceptsBankTransfer">
      <el-switch
        v-model="form.values.acceptsBankTransfer"
        :active-text="t('enums.paymentMethod.bankTransfer')"
        :aria-label="t('enums.paymentMethod.bankTransfer')"
        data-testid="settings-bank"
      />
    </AppField>
    <AppField
      v-if="form.values.acceptsBankTransfer"
      name="bankTransferInstructions"
      :label="t('admin.settings.bankInstructions')"
    >
      <el-input
        v-model="form.values.bankTransferInstructions"
        type="textarea"
        :rows="4"
        maxlength="2000"
        data-testid="settings-bank-text"
      />
    </AppField>
    <AppField
      name="refundApprovalThreshold"
      :label="t('admin.settings.refundThreshold')"
      :hint="t('admin.settings.refundThresholdHint')"
    >
      <el-input v-model="form.values.refundApprovalThreshold" inputmode="decimal" />
    </AppField>
    <AppButton
      variant="primary"
      type="submit"
      :loading="form.submitting.value"
      data-testid="settings-payment-save"
    >
      {{ t('common.save') }}
    </AppButton>
  </AppForm>
</template>
