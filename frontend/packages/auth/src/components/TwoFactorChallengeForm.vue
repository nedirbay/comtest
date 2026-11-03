<script setup lang="ts">
import { AppButton, AppField, AppForm, OtpInput } from '@comtest/ui'
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { otpSchema, recoveryCodeSchema } from '../schemas'
import { useAuthStore } from '../store'
import { useAuthForm } from '../useAuthForm'

const props = defineProps<{ challengeToken: string }>()
const emit = defineEmits<{ done: []; back: [] }>()

const { t } = useI18n({ useScope: 'global' })
const auth = useAuthStore()
const useRecovery = ref(false)

const codeForm = useAuthForm({
  schema: otpSchema,
  initial: { code: '' },
  submit: async ({ code }) => {
    await auth.completeTwoFactor(props.challengeToken, { code })
    emit('done')
  },
})

const recoveryForm = useAuthForm({
  schema: recoveryCodeSchema,
  initial: { recoveryCode: '' },
  submit: async ({ recoveryCode }) => {
    await auth.completeTwoFactor(props.challengeToken, { recoveryCode })
    emit('done')
  },
})
</script>

<template>
  <div>
    <p class="mb-5 text-sm text-ink-muted">
      {{ useRecovery ? t('auth.twoFactor.recoveryIntro') : t('auth.twoFactor.intro') }}
    </p>

    <AppForm v-if="!useRecovery" :error="codeForm.formError.value" @submit="codeForm.handleSubmit">
      <AppField name="code" :label="t('auth.twoFactor.code')">
        <OtpInput v-model="codeForm.values.code" @finish="codeForm.handleSubmit" />
      </AppField>
      <AppButton type="submit" variant="primary" block :loading="codeForm.submitting.value">
        {{ t('auth.twoFactor.submit') }}
      </AppButton>
    </AppForm>

    <AppForm v-else :error="recoveryForm.formError.value" @submit="recoveryForm.handleSubmit">
      <AppField name="recoveryCode" :label="t('auth.twoFactor.recoveryCode')">
        <el-input v-model="recoveryForm.values.recoveryCode" autocomplete="one-time-code" />
      </AppField>
      <AppButton type="submit" variant="primary" block :loading="recoveryForm.submitting.value">
        {{ t('auth.twoFactor.submit') }}
      </AppButton>
    </AppForm>

    <div class="mt-4 flex items-center justify-between text-sm">
      <el-button link @click="emit('back')">{{ t('common.back') }}</el-button>
      <el-button link type="primary" @click="useRecovery = !useRecovery">
        {{ useRecovery ? t('auth.twoFactor.useApp') : t('auth.twoFactor.useRecovery') }}
      </el-button>
    </div>
  </div>
</template>
