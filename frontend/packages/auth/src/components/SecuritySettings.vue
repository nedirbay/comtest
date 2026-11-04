<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import ChangeEmailForm from './ChangeEmailForm.vue'
import ChangePasswordForm from './ChangePasswordForm.vue'
import PhoneVerificationForm from './PhoneVerificationForm.vue'
import SessionsList from './SessionsList.vue'
import TwoFactorSection from './TwoFactorSection.vue'

const { t } = useI18n({ useScope: 'global' })
</script>

<template>
  <div class="space-y-5">
    <section class="rounded-card border border-line bg-surface p-5">
      <h2 class="mb-4 text-base font-bold text-ink">{{ t('auth.security.twoFactorTitle') }}</h2>
      <TwoFactorSection />
    </section>
    <section class="rounded-card border border-line bg-surface p-5">
      <h2 class="mb-4 text-base font-bold text-ink">{{ t('auth.password.title') }}</h2>
      <ChangePasswordForm />
    </section>
    <section class="rounded-card border border-line bg-surface p-5">
      <h2 class="mb-4 text-base font-bold text-ink">{{ t('auth.emailChange.title') }}</h2>
      <ChangeEmailForm />
    </section>
    <section class="rounded-card border border-line bg-surface p-5">
      <h2 class="mb-4 text-base font-bold text-ink">{{ t('auth.phone.title') }}</h2>
      <PhoneVerificationForm />
    </section>
    <section>
      <h2 class="mb-1 text-base font-bold text-ink">{{ t('auth.sessions.title') }}</h2>
      <p class="mb-4 text-sm text-ink-muted">{{ t('auth.sessions.intro') }}</p>
      <SessionsList />
    </section>
  </div>
</template>
