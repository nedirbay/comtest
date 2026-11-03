<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { AppButton, AppField, AppForm } from '@comtest/ui'
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { takeFragmentParams } from '../navigation'
import { resetPasswordSchema } from '../schemas'
import { useApi } from '../store'
import { useAuthForm } from '../useAuthForm'

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const link = ref<{ userId: string; token: string } | null>(null)
const state = ref<'checking' | 'ready' | 'invalid' | 'done'>('checking')

onMounted(() => {
  const params = takeFragmentParams()
  const userId = params.get('userId')
  const token = params.get('token')

  if (userId && token) {
    link.value = { userId, token }
    state.value = 'ready'
  } else {
    state.value = 'invalid'
  }
})

const form = useAuthForm({
  schema: resetPasswordSchema,
  initial: { password: '', confirm: '' },
  submit: async ({ password }) => {
    await unwrap(
      api.client.POST('/api/v1/auth/reset-password', {
        body: { ...link.value!, newPassword: password },
      }),
    )
    form.reset()
    state.value = 'done'
  },
})
</script>

<template>
  <div v-if="state === 'checking'" class="h-40" />

  <el-result v-else-if="state === 'invalid'" icon="error" :title="t('auth.reset.invalidTitle')">
    <template #sub-title>
      <p class="text-sm text-ink-muted">{{ t('auth.reset.invalidText') }}</p>
    </template>
    <template #extra><slot name="restart" /></template>
  </el-result>

  <el-result v-else-if="state === 'done'" icon="success" :title="t('auth.reset.doneTitle')">
    <template #sub-title>
      <p class="text-sm text-ink-muted">{{ t('auth.reset.doneText') }}</p>
    </template>
    <template #extra><slot name="login" /></template>
  </el-result>

  <AppForm v-else :error="form.formError.value" @submit="form.handleSubmit">
    <AppField
      name="password"
      :label="t('auth.reset.newPassword')"
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
    <AppField name="confirm" :label="t('auth.reset.confirmPassword')">
      <el-input
        v-model="form.values.confirm"
        name="confirm-password"
        type="password"
        autocomplete="new-password"
        show-password
        size="large"
      />
    </AppField>
    <AppButton type="submit" variant="primary" size="large" block :loading="form.submitting.value">
      {{ t('auth.reset.submit') }}
    </AppButton>
  </AppForm>
</template>
