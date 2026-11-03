<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import {
  AppButton,
  AppDialog,
  ErrorState,
  SkeletonBlock,
  formatDateTime,
  useApiErrorMessage,
} from '@comtest/ui'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useApi } from '../store'
import { describeDevice } from '../device'

type Session = Schema<'SessionDto'>

const { t, locale } = useI18n({ useScope: 'global' })
const api = useApi()
const queryClient = useQueryClient()
const describeError = useApiErrorMessage()

const QUERY_KEY = ['account', 'sessions']

const sessions = useQuery({
  queryKey: QUERY_KEY,
  queryFn: () => unwrap(api.client.GET('/api/v1/account/sessions')),
})

const list = computed<readonly Session[]>(() => sessions.data.value ?? [])
const hasOthers = computed(() => list.value.some((session) => !session.isCurrent))

const target = ref<Session | 'others' | null>(null)
const dialogOpen = computed({
  get: () => target.value !== null,
  set: (open) => {
    if (!open) {
      target.value = null
    }
  },
})

const revoke = useMutation({
  mutationFn: (what: Session | 'others') =>
    what === 'others'
      ? unwrap(api.client.DELETE('/api/v1/account/sessions'))
      : unwrap(
          api.client.DELETE('/api/v1/account/sessions/{id}', { params: { path: { id: what.id } } }),
        ),
  onSuccess: () => {
    target.value = null

    return queryClient.invalidateQueries({ queryKey: QUERY_KEY })
  },
})
</script>

<template>
  <div>
    <ErrorState
      v-if="sessions.isError.value"
      :error="sessions.error.value"
      retryable
      @retry="sessions.refetch()"
    />
    <SkeletonBlock v-else-if="sessions.isPending.value" :lines="4" />
    <template v-else>
      <ul class="divide-y divide-line rounded-card border border-line bg-surface">
        <li
          v-for="session in list"
          :key="session.id"
          class="flex flex-wrap items-center justify-between gap-3 p-4"
          data-testid="session"
        >
          <div class="min-w-0">
            <p class="flex flex-wrap items-center gap-2 text-sm font-semibold text-ink">
              {{ describeDevice(session.userAgent) }}
              <el-tag v-if="session.isCurrent" type="success" size="small" disable-transitions>
                {{ t('auth.sessions.current') }}
              </el-tag>
            </p>
            <p class="mt-0.5 text-xs text-ink-subtle">
              {{ session.ipAddress ?? '—' }} ·
              {{
                t('auth.sessions.lastSeen', { date: formatDateTime(session.lastSeenAt, locale) })
              }}
            </p>
          </div>
          <AppButton v-if="!session.isCurrent" size="small" @click="target = session">
            {{ t('auth.sessions.revoke') }}
          </AppButton>
        </li>
      </ul>
      <AppButton v-if="hasOthers" class="mt-4" variant="danger" plain @click="target = 'others'">
        {{ t('auth.sessions.revokeOthers') }}
      </AppButton>
    </template>

    <AppDialog
      v-model="dialogOpen"
      :title="target === 'others' ? t('auth.sessions.revokeOthers') : t('auth.sessions.revoke')"
      confirm-variant="danger"
      :confirm-text="t('auth.sessions.revoke')"
      :loading="revoke.isPending.value"
      @confirm="target && revoke.mutate(target)"
    >
      <p class="text-sm text-ink-muted">{{ t('auth.sessions.revokeWarning') }}</p>
      <p v-if="revoke.isError.value" class="mt-3 text-sm text-danger" role="alert">
        {{ describeError(revoke.error.value) }}
      </p>
    </AppDialog>
  </div>
</template>
