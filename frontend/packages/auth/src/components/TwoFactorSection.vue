<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { AppButton, AppDialog, AppField, AppForm, OtpInput } from '@comtest/ui'
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { disableTwoFactorSchema, otpSchema } from '../schemas'
import { useApi, useAuthStore } from '../store'
import { useAuthForm } from '../useAuthForm'
import RecoveryCodesPanel from './RecoveryCodesPanel.vue'
import TwoFactorSetupPanel from './TwoFactorSetupPanel.vue'

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()

type Dialog = 'setup' | 'disable' | 'regenerate' | 'codes' | null

const dialog = ref<Dialog>(null)
const codes = ref<string[]>([])
const isOn = computed(() => auth.profile?.twoFactorEnabled === true)

const titles = computed<Record<Exclude<Dialog, null>, string>>(() => ({
  setup: t('auth.setup.title'),
  disable: t('auth.security.disable'),
  regenerate: t('auth.security.regenerate'),
  codes: t('auth.recovery.title'),
}))

const open = computed({
  get: () => dialog.value !== null,
  set: (value) => {
    if (!value) {
      dialog.value = null
    }
  },
})

const disableForm = useAuthForm({
  schema: disableTwoFactorSchema,
  initial: { password: '', code: '' },
  submit: async (body) => {
    await unwrap(api.client.POST('/api/v1/account/two-factor/disable', { body }))
    disableForm.reset()
    dialog.value = null
    await auth.refreshSession()
  },
})

const regenerateForm = useAuthForm({
  schema: otpSchema,
  initial: { code: '' },
  submit: async (body) => {
    const result = await unwrap(
      api.client.POST('/api/v1/account/two-factor/recovery-codes', { body }),
    )

    regenerateForm.reset()
    codes.value = [...result.codes]
    dialog.value = 'codes'
  },
})
</script>

<template>
  <div class="flex flex-wrap items-center justify-between gap-4">
    <p class="flex items-center gap-2 text-sm text-ink-muted">
      <el-tag :type="isOn ? 'success' : 'info'" disable-transitions size="small">
        {{ isOn ? '✓' : '—' }}
      </el-tag>
      {{ isOn ? t('auth.security.twoFactorOn') : t('auth.security.twoFactorOff') }}
    </p>
    <div class="flex flex-wrap gap-2">
      <template v-if="isOn">
        <AppButton @click="dialog = 'regenerate'">{{ t('auth.security.regenerate') }}</AppButton>
        <AppButton variant="danger" plain @click="dialog = 'disable'">
          {{ t('auth.security.disable') }}
        </AppButton>
      </template>
      <AppButton v-else variant="primary" @click="dialog = 'setup'">
        {{ t('auth.security.enable') }}
      </AppButton>
    </div>

    <AppDialog v-if="dialog" v-model="open" :title="titles[dialog]">
      <TwoFactorSetupPanel v-if="dialog === 'setup'" @done="dialog = null" />
      <RecoveryCodesPanel v-else-if="dialog === 'codes'" :codes="codes" @done="dialog = null" />

      <AppForm
        v-else-if="dialog === 'disable'"
        id="disable-two-factor"
        :error="disableForm.formError.value"
        @submit="disableForm.handleSubmit"
      >
        <p class="mb-4 text-sm text-ink-muted">{{ t('auth.security.disableText') }}</p>
        <AppField name="password" :label="t('auth.fields.password')">
          <el-input
            v-model="disableForm.values.password"
            type="password"
            autocomplete="current-password"
            show-password
          />
        </AppField>
        <AppField name="code" :label="t('auth.twoFactor.code')">
          <OtpInput v-model="disableForm.values.code" />
        </AppField>
      </AppForm>

      <AppForm
        v-else
        id="regenerate-codes"
        :error="regenerateForm.formError.value"
        @submit="regenerateForm.handleSubmit"
      >
        <p class="mb-4 text-sm text-ink-muted">{{ t('auth.security.regenerateText') }}</p>
        <AppField name="code" :label="t('auth.twoFactor.code')">
          <OtpInput v-model="regenerateForm.values.code" />
        </AppField>
      </AppForm>

      <template #actions>
        <template v-if="dialog === 'disable'">
          <AppButton @click="dialog = null">{{ t('common.cancel') }}</AppButton>
          <AppButton
            variant="danger"
            :loading="disableForm.submitting.value"
            @click="disableForm.handleSubmit"
          >
            {{ t('auth.security.disable') }}
          </AppButton>
        </template>
        <template v-else-if="dialog === 'regenerate'">
          <AppButton @click="dialog = null">{{ t('common.cancel') }}</AppButton>
          <AppButton
            variant="primary"
            :loading="regenerateForm.submitting.value"
            @click="regenerateForm.handleSubmit"
          >
            {{ t('common.confirm') }}
          </AppButton>
        </template>
        <span v-else />
      </template>
    </AppDialog>
  </div>
</template>
