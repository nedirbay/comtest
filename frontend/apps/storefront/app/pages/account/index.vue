<script setup lang="ts">
import { useAuthStore } from '@comtest/auth'
import { formatPhone } from '@comtest/ui'

definePageMeta({ middleware: 'auth', requiresAuth: true })

const { t } = useI18n()
const auth = useAuthStore()

useHead({ title: computed(() => t('store.account.title')) })
</script>

<template>
  <AccountShell :title="t('store.account.title')">
    <section v-if="auth.profile" class="max-w-xl rounded-card border border-line bg-surface p-5">
      <h2 class="mb-4 text-base font-bold text-ink">{{ t('store.account.profile') }}</h2>
      <dl class="space-y-3 text-sm">
        <div>
          <dt class="text-ink-subtle">{{ t('store.account.name') }}</dt>
          <dd class="font-semibold text-ink" data-testid="profile-name">
            {{ auth.profile.fullName }}
          </dd>
        </div>
        <div>
          <dt class="text-ink-subtle">{{ t('store.account.email') }}</dt>
          <dd class="font-semibold text-ink">{{ auth.profile.email }}</dd>
        </div>
        <div>
          <dt class="text-ink-subtle">{{ t('store.account.phone') }}</dt>
          <dd class="font-semibold text-ink">
            {{
              auth.profile.phoneNumber
                ? formatPhone(auth.profile.phoneNumber)
                : t('store.account.notSet')
            }}
          </dd>
        </div>
      </dl>
    </section>
  </AccountShell>
</template>
