<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { AppButton, AppField, AppForm, formatPhone, normalizePhone, OtpInput } from '@comtest/ui'
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { z } from '@comtest/ui/zod'
import { otpSchema } from '../schemas'
import { useApi, useAuthStore } from '../store'
import { useAuthForm } from '../useAuthForm'

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const sentTo = ref<string | null>(null)

const phoneSchema = z.object({
  phoneNumber: z
    .string()
    .transform((value) => normalizePhone(value) ?? '')
    .refine((value) => value !== '', 'validation.phone'),
})

const phoneForm = useAuthForm({
  schema: phoneSchema,
  initial: { phoneNumber: '' },
  submit: async (body) => {
    await unwrap(api.client.POST('/api/v1/account/phone/send-code', { body }))
    sentTo.value = body.phoneNumber
  },
})

const codeForm = useAuthForm({
  schema: otpSchema,
  initial: { code: '' },
  submit: async (body) => {
    await unwrap(api.client.POST('/api/v1/account/phone/verify', { body }))
    await auth.loadProfile()
    sentTo.value = null
    codeForm.reset()
    phoneForm.reset()
  },
})
</script>

<template>
  <div class="max-w-md">
    <p v-if="auth.profile?.phoneNumberConfirmed" class="mb-4 text-sm text-ink-muted">
      {{ t('auth.phone.current', { phone: formatPhone(auth.profile.phoneNumber ?? '') }) }}
    </p>

    <AppForm v-if="!sentTo" :error="phoneForm.formError.value" @submit="phoneForm.handleSubmit">
      <AppField name="phoneNumber" :label="t('auth.phone.number')">
        <el-input
          v-model="phoneForm.values.phoneNumber"
          type="tel"
          autocomplete="tel"
          placeholder="+993 65 123456"
        />
      </AppField>
      <AppButton type="submit" variant="primary" :loading="phoneForm.submitting.value">
        {{ t('auth.phone.send') }}
      </AppButton>
    </AppForm>

    <AppForm v-else :error="codeForm.formError.value" @submit="codeForm.handleSubmit">
      <p class="mb-4 text-sm text-ink-muted">
        {{ t('auth.phone.sent', { phone: formatPhone(sentTo) }) }}
      </p>
      <AppField name="code" :label="t('auth.phone.code')">
        <OtpInput v-model="codeForm.values.code" />
      </AppField>
      <div class="flex gap-2">
        <AppButton type="submit" variant="primary" :loading="codeForm.submitting.value">
          {{ t('common.confirm') }}
        </AppButton>
        <AppButton @click="sentTo = null">{{ t('common.back') }}</AppButton>
      </div>
    </AppForm>
  </div>
</template>
