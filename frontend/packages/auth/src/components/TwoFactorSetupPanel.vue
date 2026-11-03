<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { AppButton, AppField, AppForm, ErrorState, SkeletonBlock, OtpInput } from '@comtest/ui'
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { otpSchema } from '../schemas'
import { useApi, useAuthStore } from '../store'
import { useAuthForm } from '../useAuthForm'
import RecoveryCodesPanel from './RecoveryCodesPanel.vue'

const emit = defineEmits<{ done: [] }>()

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()

const setup = ref<Schema<'TwoFactorSetupDto'> | null>(null)
const qrCode = ref<string | null>(null)
const loadError = ref<unknown>(null)
const recoveryCodes = ref<string[] | null>(null)

async function begin(): Promise<void> {
  loadError.value = null

  try {
    setup.value = await unwrap(api.client.POST('/api/v1/account/two-factor/setup'))

    // Drawn in the browser, because the key must not be handed to any outside QR code service.
    const { toString } = await import('qrcode')

    const svg = await toString(setup.value.authenticatorUri, { type: 'svg', margin: 1 })

    qrCode.value = `data:image/svg+xml,${encodeURIComponent(svg)}`
  } catch (error) {
    loadError.value = error
  }
}

onMounted(begin)

const form = useAuthForm({
  schema: otpSchema,
  initial: { code: '' },
  submit: async (body) => {
    const result = await unwrap(api.client.POST('/api/v1/account/two-factor/enable', { body }))

    recoveryCodes.value = [...result.codes]
    await auth.refreshSession()
  },
})
</script>

<template>
  <RecoveryCodesPanel v-if="recoveryCodes" :codes="recoveryCodes" @done="emit('done')" />
  <ErrorState v-else-if="loadError" :error="loadError" retryable @retry="begin" />
  <SkeletonBlock v-else-if="!setup" :lines="6" />
  <div v-else>
    <ol class="mb-5 list-decimal space-y-1.5 pl-5 text-sm text-ink-muted">
      <li>{{ t('auth.setup.step1') }}</li>
      <li>{{ t('auth.setup.step2') }}</li>
      <li>{{ t('auth.setup.step3') }}</li>
    </ol>
    <div class="mb-5 flex flex-col items-center gap-3 rounded-control border border-line p-4">
      <img
        v-if="qrCode"
        :src="qrCode"
        :alt="t('auth.setup.step2')"
        class="size-44 bg-white p-1"
        data-testid="qr"
      />
      <div class="w-full text-center">
        <p class="mb-1 text-xs text-ink-subtle">{{ t('auth.setup.manualKey') }}</p>
        <code class="text-sm break-all select-all" data-testid="shared-key">
          {{ setup.sharedKey }}
        </code>
      </div>
    </div>
    <AppForm :error="form.formError.value" @submit="form.handleSubmit">
      <AppField name="code" :label="t('auth.twoFactor.code')">
        <OtpInput v-model="form.values.code" />
      </AppField>
      <AppButton type="submit" variant="primary" block :loading="form.submitting.value">
        {{ t('auth.setup.submit') }}
      </AppButton>
    </AppForm>
  </div>
</template>
