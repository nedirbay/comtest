<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { AppButton, AppField, AppForm } from '@comtest/ui'
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { emailSchema } from '../schemas'
import { useApi } from '../store'
import { useAuthForm } from '../useAuthForm'

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const sent = ref(false)

const form = useAuthForm({
  schema: emailSchema,
  initial: { email: '' },
  submit: async (body) => {
    await unwrap(api.client.POST('/api/v1/auth/forgot-password', { body }))
    sent.value = true
  },
})
</script>

<template>
  <el-result v-if="sent" icon="success" :title="t('auth.forgot.sentTitle')">
    <template #sub-title>
      <p class="text-sm text-ink-muted">{{ t('auth.forgot.sentText') }}</p>
    </template>
  </el-result>

  <AppForm v-else :error="form.formError.value" @submit="form.handleSubmit">
    <p class="mb-5 text-sm text-ink-muted">{{ t('auth.forgot.intro') }}</p>
    <AppField name="email" :label="t('auth.fields.email')">
      <el-input
        v-model="form.values.email"
        name="email"
        type="email"
        autocomplete="email"
        autocapitalize="none"
        spellcheck="false"
        size="large"
      />
    </AppField>
    <AppButton type="submit" variant="primary" size="large" block :loading="form.submitting.value">
      {{ t('auth.forgot.submit') }}
    </AppButton>
  </AppForm>
</template>
