<script setup lang="ts">
import { ProfileForm, useAuthStore } from '@comtest/auth'
import { formatPhone } from '@comtest/ui'

definePageMeta({ middleware: 'auth', requiresAuth: true })

const { t } = useI18n()
const auth = useAuthStore()
const localePath = useLocalePath()

useHead({ title: computed(() => t('store.account.title')) })
</script>

<template>
  <AccountShell :title="t('store.account.title')">
    <section v-if="auth.profile" class="max-w-xl rounded-card border border-line bg-surface p-5">
      <h2 class="mb-4 text-base font-bold text-ink">{{ t('store.account.profile') }}</h2>
      <ProfileForm />
      <dl class="mt-6 space-y-3 border-t border-line pt-4 text-sm">
        <div>
          <dt class="text-ink-subtle">{{ t('store.account.email') }}</dt>
          <dd class="font-semibold text-ink" data-testid="profile-email">
            {{ auth.profile.email }}
          </dd>
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
      <NuxtLink
        :to="localePath('/account/security')"
        class="mt-3 inline-block text-sm font-semibold text-primary"
        data-testid="change-contact"
      >
        {{ t('store.account.changeContact') }}
      </NuxtLink>
    </section>
  </AccountShell>
</template>
