<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { AppButton, AppField, AppForm } from '@comtest/ui'
import { useI18n } from 'vue-i18n'
import { z } from '@comtest/ui/zod'
import { useAdminForm } from '@/shared/composables/useAdminForm'
import { toastSuccess } from '@/shared/toast'
import { nullIfEmpty, optionalAmount, optionalText, toAmount } from '@/shared/validation'

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
  // What a new store starts with. The API has no way to read the current choice back.
  initial: {
    acceptsCashOnDelivery: true,
    acceptsBankTransfer: false,
    bankTransferInstructions: '',
    refundApprovalThreshold: '',
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
    toastSuccess(t('admin.settings.saved'))
  },
})
</script>

<template>
  <AppForm :error="form.formError.value" class="max-w-xl" @submit="form.handleSubmit()">
    <el-alert
      type="warning"
      show-icon
      :closable="false"
      class="mb-4"
      :title="t('admin.settings.paymentNoReadTitle')"
      :description="t('admin.settings.paymentNoReadText')"
      data-testid="payment-no-read"
    />
    <AppField name="acceptsCashOnDelivery">
      <el-switch
        v-model="form.values.acceptsCashOnDelivery"
        :active-text="t('enums.paymentMethod.cashOnDelivery')"
        data-testid="settings-cod"
      />
    </AppField>
    <AppField name="acceptsBankTransfer">
      <el-switch
        v-model="form.values.acceptsBankTransfer"
        :active-text="t('enums.paymentMethod.bankTransfer')"
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
