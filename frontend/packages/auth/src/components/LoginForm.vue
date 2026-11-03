<script setup lang="ts">
import { ErrorCode, isApiError } from '@comtest/api-client'
import { AppButton, AppField, AppForm } from '@comtest/ui'
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { loginSchema } from '../schemas'
import { useAuthStore } from '../store'
import { useAuthForm } from '../useAuthForm'
import TwoFactorChallengeForm from './TwoFactorChallengeForm.vue'
import ResendConfirmation from './ResendConfirmation.vue'

const emit = defineEmits<{ 'signed-in': []; 'stage-change': [stage: 'password' | 'two-factor'] }>()

const { t } = useI18n({ useScope: 'global' })
const auth = useAuthStore()
const challengeToken = ref<string | null>(null)

const form = useAuthForm({
  schema: loginSchema,
  initial: { login: '', password: '' },
  submit: async ({ login, password }) => {
    const outcome = await auth.login(login, password)

    // Nothing else needs the password from here on, so it should not stay in memory.
    form.values.password = ''

    if (outcome.kind === 'two-factor') {
      challengeToken.value = outcome.challengeToken
      emit('stage-change', 'two-factor')
    } else {
      emit('signed-in')
    }
  },
})

const emailNotConfirmed = computed(() =>
  isApiError(form.lastError.value, ErrorCode.EmailNotConfirmed),
)

function backToPassword(): void {
  challengeToken.value = null
  emit('stage-change', 'password')
}
</script>

<template>
  <TwoFactorChallengeForm
    v-if="challengeToken"
    :challenge-token="challengeToken"
    @done="emit('signed-in')"
    @back="backToPassword"
  />

  <AppForm v-else :error="form.formError.value" @submit="form.handleSubmit">
    <AppField name="login" :label="t('auth.fields.login')">
      <el-input
        v-model="form.values.login"
        name="username"
        autocomplete="username"
        autocapitalize="none"
        spellcheck="false"
        size="large"
      />
    </AppField>
    <AppField name="password" :label="t('auth.fields.password')">
      <el-input
        v-model="form.values.password"
        name="password"
        type="password"
        autocomplete="current-password"
        show-password
        size="large"
      />
    </AppField>
    <div class="-mt-2 mb-5 text-right text-sm"><slot name="forgot" /></div>
    <AppButton type="submit" variant="primary" size="large" block :loading="form.submitting.value">
      {{ t('auth.login.submit') }}
    </AppButton>
    <ResendConfirmation v-if="emailNotConfirmed" :email="form.values.login" class="mt-4" />
  </AppForm>
</template>
