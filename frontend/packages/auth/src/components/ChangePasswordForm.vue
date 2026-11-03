<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { AppButton, AppField, AppForm } from '@comtest/ui'
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { changePasswordSchema } from '../schemas'
import { useApi } from '../store'
import { useAuthForm } from '../useAuthForm'

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const done = ref(false)

const form = useAuthForm({
  schema: changePasswordSchema,
  initial: { currentPassword: '', newPassword: '', confirm: '' },
  submit: async ({ currentPassword, newPassword }) => {
    done.value = false
    await unwrap(
      api.client.POST('/api/v1/account/password', { body: { currentPassword, newPassword } }),
    )
    form.reset()
    done.value = true
  },
})
</script>

<template>
  <AppForm :error="form.formError.value" class="max-w-md" @submit="form.handleSubmit">
    <el-alert
      v-if="done"
      :title="t('auth.password.changed')"
      type="success"
      show-icon
      :closable="false"
      class="mb-4"
    />
    <AppField name="currentPassword" :label="t('auth.password.current')">
      <el-input
        v-model="form.values.currentPassword"
        type="password"
        autocomplete="current-password"
        show-password
      />
    </AppField>
    <AppField
      name="newPassword"
      :label="t('auth.reset.newPassword')"
      :hint="t('auth.fields.passwordHint')"
    >
      <el-input
        v-model="form.values.newPassword"
        type="password"
        autocomplete="new-password"
        show-password
      />
    </AppField>
    <AppField name="confirm" :label="t('auth.reset.confirmPassword')">
      <el-input
        v-model="form.values.confirm"
        type="password"
        autocomplete="new-password"
        show-password
      />
    </AppField>
    <AppButton type="submit" variant="primary" :loading="form.submitting.value">
      {{ t('auth.password.submit') }}
    </AppButton>
  </AppForm>
</template>
