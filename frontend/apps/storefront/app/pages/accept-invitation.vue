<script setup lang="ts">
import { isApiError, unwrap } from '@comtest/api-client'
import { useApi, useAuthStore } from '@comtest/auth'
import { AppButton, SkeletonBlock, useApiErrorMessage } from '@comtest/ui'

definePageMeta({ layout: 'auth' })

const { t } = useI18n()
const localePath = useLocalePath()
const config = useRuntimeConfig()
const api = useApi()
const auth = useAuthStore()
const describeError = useApiErrorMessage()

// In memory, so the token survives the trip through the sign-in page but not a closed tab.
const token = useState<string | null>('invitation-token', () => null)
type State = 'working' | 'needs-login' | 'no-token' | 'done' | 'failed'
const state = ref<State>('working')
const message = ref('')

useHead({ title: computed(() => t('store.invitation.title')) })

onMounted(async () => {
  const fromLink = new URLSearchParams(window.location.hash.slice(1)).get('token')

  if (fromLink) {
    token.value = fromLink
    window.history.replaceState(window.history.state, '', window.location.pathname)
  }

  if (!token.value) {
    state.value = 'no-token'

    return
  }

  await auth.bootstrap()

  if (!auth.isAuthenticated) {
    state.value = 'needs-login'

    return
  }

  try {
    await unwrap(
      api.client.POST('/api/v1/tenants/invitations/accept', { body: { token: token.value } }),
    )
    token.value = null
    state.value = 'done'
  } catch (error) {
    state.value = 'failed'
    message.value = isApiError(error) ? describeError(error) : t('errors.unexpected')
  }
})

const returnHere = computed(() => localePath('/accept-invitation'))
</script>

<template>
  <h1 class="mb-4 text-2xl font-bold tracking-tight text-ink">{{ t('store.invitation.title') }}</h1>
  <SkeletonBlock v-if="state === 'working'" />
  <p v-else-if="state === 'no-token'" class="text-sm text-ink-muted">
    {{ t('store.invitation.noToken') }}
  </p>
  <div v-else-if="state === 'needs-login'" class="space-y-4" data-testid="invitation-login">
    <p class="text-sm text-ink-muted">{{ t('store.invitation.needsLogin') }}</p>
    <NuxtLink :to="localePath({ path: '/login', query: { returnUrl: returnHere } })" class="block">
      <AppButton variant="primary" block>{{ t('store.nav.login') }}</AppButton>
    </NuxtLink>
    <NuxtLink :to="localePath('/register')" class="block">
      <AppButton block>{{ t('auth.register.submit') }}</AppButton>
    </NuxtLink>
  </div>
  <el-result
    v-else-if="state === 'done'"
    icon="success"
    :title="t('store.invitation.doneTitle')"
    :sub-title="t('store.invitation.doneText')"
  >
    <template #extra>
      <a :href="config.public.adminUrl">
        <AppButton variant="primary" data-testid="open-admin">
          {{ t('store.invitation.openAdmin') }}
        </AppButton>
      </a>
    </template>
  </el-result>
  <el-result v-else icon="error" :title="t('store.invitation.failedTitle')" :sub-title="message" />
</template>
