<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { useApi, useAuthStore } from '@comtest/auth'
import { AppButton, EmptyState, ErrorState, SkeletonBlock, useApiErrorMessage } from '@comtest/ui'
import { useQuery } from '@tanstack/vue-query'
import { ref, watchEffect } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'

const { t, locale } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const router = useRouter()
const describeError = useApiErrorMessage()

const switching = ref<string | null>(null)
const switchError = ref<string | null>(null)

const memberships = useQuery({
  queryKey: ['account', 'tenants'],
  queryFn: () => unwrap(api.client.GET('/api/v1/account/tenants')),
})

async function choose(tenantId: string): Promise<void> {
  switching.value = tenantId
  switchError.value = null

  try {
    await auth.switchTenant(tenantId)
    await router.replace(`/${locale.value}`)
  } catch (error) {
    switchError.value = describeError(error)
  } finally {
    switching.value = null
  }
}

// With a single store there is nothing to choose, so the page only flashes by.
watchEffect(() => {
  const list = memberships.data.value

  if (list?.length === 1 && list[0] && switching.value === null && switchError.value === null) {
    void choose(list[0].tenantId)
  }
})
</script>

<template>
  <h1 class="mb-2 text-2xl font-bold tracking-tight text-ink">
    {{ t('admin.selectStore.title') }}
  </h1>
  <ErrorState
    v-if="memberships.isError.value"
    :error="memberships.error.value"
    retryable
    @retry="memberships.refetch()"
  />
  <SkeletonBlock v-else-if="memberships.isPending.value" />
  <template v-else-if="memberships.data.value?.length">
    <p class="mb-5 text-sm text-ink-muted">{{ t('admin.selectStore.intro') }}</p>
    <p v-if="switchError" class="mb-3 text-sm text-danger" role="alert">{{ switchError }}</p>
    <ul class="space-y-2">
      <li v-for="store in memberships.data.value" :key="store.tenantId">
        <button
          type="button"
          class="flex w-full items-center justify-between rounded-card border border-line bg-surface p-4 text-left transition-colors hover:border-primary disabled:opacity-60"
          :disabled="switching !== null"
          @click="choose(store.tenantId)"
        >
          <span class="font-semibold text-ink">{{ store.tenantName }}</span>
          <span class="text-xs text-ink-subtle">{{ store.role }}</span>
        </button>
      </li>
    </ul>
  </template>
  <EmptyState v-else :description="t('admin.selectStore.none')">
    <AppButton @click="auth.logout()">{{ t('auth.logout') }}</AppButton>
  </EmptyState>
</template>
