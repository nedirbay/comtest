<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { AppButton, AppField, AppForm } from '@comtest/ui'
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { registerSchema } from '../schemas'
import { useApi } from '../store'
import { useAuthForm } from '../useAuthForm'

const { t, locale } = useI18n({ useScope: 'global' })
const api = useApi()
const sentTo = ref<string | null>(null)

const form = useAuthForm({
  schema: registerSchema,
  initial: { email: '', fullName: '', password: '' },
  submit: async (values) => {
    await unwrap(
      api.client.POST('/api/v1/auth/register', { body: { ...values, locale: locale.value } }),
    )
    sentTo.value = values.email
    form.values.password = ''
  },
})
</script>

<template>
  <el-result v-if="sentTo" icon="success" :title="t('auth.register.sentTitle')">
    <template #sub-title>
      <p class="text-sm text-ink-muted">{{ t('auth.register.sentText', { email: sentTo }) }}</p>
    </template>
  </el-result>

  <AppForm v-else :error="form.formError.value" @submit="form.handleSubmit">
    <AppField name="fullName" :label="t('auth.fields.fullName')">
      <el-input v-model="form.values.fullName" name="name" autocomplete="name" size="large" />
    </AppField>
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
    <AppField
      name="password"
      :label="t('auth.fields.password')"
      :hint="t('auth.fields.passwordHint')"
    >
      <el-input
        v-model="form.values.password"
        name="new-password"
        type="password"
        autocomplete="new-password"
        show-password
        size="large"
      />
    </AppField>
    <AppButton type="submit" variant="primary" size="large" block :loading="form.submitting.value">
      {{ t('auth.register.submit') }}
    </AppButton>
  </AppForm>
</template>
