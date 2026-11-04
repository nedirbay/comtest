<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { AppButton, AppField, AppForm } from '@comtest/ui'
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { changeEmailSchema } from '../schemas'
import { useApi, useAuthStore } from '../store'
import { useAuthForm } from '../useAuthForm'

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const sentTo = ref<string | null>(null)

const form = useAuthForm({
  schema: changeEmailSchema,
  initial: { newEmail: '', password: '' },
  submit: async (values) => {
    sentTo.value = null
    await unwrap(api.client.POST('/api/v1/account/email', { body: values }))
    sentTo.value = values.newEmail
    form.reset()
  },
})
</script>

<template>
  <AppForm :error="form.formError.value" class="max-w-md" @submit="form.handleSubmit">
    <p v-if="auth.profile" class="mb-1 text-sm text-ink" data-testid="current-email">
      {{ t('auth.emailChange.current', { email: auth.profile.email }) }}
    </p>
    <p class="mb-4 text-sm text-ink-muted">{{ t('auth.emailChange.intro') }}</p>
    <!-- The answer is the same when the address is taken, so that this cannot reveal who has one. -->
    <el-alert
      v-if="sentTo"
      :title="t('auth.emailChange.sent', { email: sentTo })"
      type="success"
      show-icon
      :closable="false"
      class="mb-4"
      data-testid="email-change-sent"
    />
    <AppField name="newEmail" :label="t('auth.emailChange.newEmail')">
      <el-input
        v-model="form.values.newEmail"
        type="email"
        autocomplete="email"
        data-testid="new-email"
      />
    </AppField>
    <AppField name="password" :label="t('auth.password.current')">
      <el-input
        v-model="form.values.password"
        type="password"
        autocomplete="current-password"
        show-password
        data-testid="new-email-password"
      />
    </AppField>
    <AppButton
      type="submit"
      variant="primary"
      :loading="form.submitting.value"
      data-testid="new-email-submit"
    >
      {{ t('auth.emailChange.submit') }}
    </AppButton>
  </AppForm>
</template>
