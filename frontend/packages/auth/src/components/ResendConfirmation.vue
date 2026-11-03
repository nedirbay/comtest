<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useApi } from '../store'

const props = defineProps<{ email: string }>()

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const state = ref<'idle' | 'sending' | 'sent'>('idle')

async function resend(): Promise<void> {
  state.value = 'sending'

  try {
    await unwrap(
      api.client.POST('/api/v1/auth/resend-confirmation', { body: { email: props.email } }),
    )
  } finally {
    // The answer is the same whether or not the account exists, and so is the message.
    state.value = 'sent'
  }
}
</script>

<template>
  <p v-if="state === 'sent'" class="text-sm text-ink-muted" role="status">
    {{ t('auth.confirm.resent') }}
  </p>
  <el-button v-else link type="primary" :loading="state === 'sending'" @click="resend">
    {{ t('auth.confirm.resend') }}
  </el-button>
</template>
