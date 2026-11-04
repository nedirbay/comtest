<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { useApiErrorMessage } from '@comtest/ui'
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { takeFragmentParams } from '../navigation'
import { useApi, useAuthStore } from '../store'

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const describeError = useApiErrorMessage()
const state = ref<'working' | 'done' | 'failed'>('working')
const message = ref('')

onMounted(async () => {
  const params = takeFragmentParams()
  const userId = params.get('userId')
  const newEmail = params.get('email')
  const token = params.get('token')

  if (!userId || !newEmail || !token) {
    state.value = 'failed'
    message.value = t('auth.confirm.invalidLink')

    return
  }

  try {
    // Restoring the session has to finish first, or it could race with the sessions being closed.
    await auth.bootstrap().catch(() => undefined)
    await unwrap(
      api.client.POST('/api/v1/auth/confirm-email-change', { body: { userId, newEmail, token } }),
    )

    // Every session of this account just ended. Someone else signed in here is not affected.
    if (auth.claims?.userId === userId) {
      await auth.logout().catch(() => undefined)
    }

    state.value = 'done'
  } catch (error) {
    state.value = 'failed'
    message.value = describeError(error)
  }
})
</script>

<template>
  <div v-if="state === 'working'" v-loading="true" class="h-40" :aria-label="t('common.loading')" />
  <el-result v-else-if="state === 'done'" icon="success" :title="t('auth.emailChange.doneTitle')">
    <template #sub-title>
      <p class="text-sm text-ink-muted">{{ t('auth.emailChange.doneText') }}</p>
    </template>
    <template #extra><slot name="login" /></template>
  </el-result>
  <el-result v-else icon="error" :title="t('auth.emailChange.failedTitle')">
    <template #sub-title>
      <p class="text-sm text-ink-muted">{{ message }}</p>
    </template>
    <template #extra><slot name="login" /></template>
  </el-result>
</template>
