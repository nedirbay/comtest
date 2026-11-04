<script setup lang="ts">
import { ProfileForm, SecuritySettings, useAuthStore } from '@comtest/auth'
import { PageHeader } from '@comtest/ui'
import { useI18n } from 'vue-i18n'

const { t } = useI18n({ useScope: 'global' })
const auth = useAuthStore()
</script>

<template>
  <PageHeader :title="t('auth.security.title')" :description="t('auth.security.description')" />
  <section v-if="auth.profile" class="mb-5 rounded-card border border-line bg-surface p-5">
    <h2 class="mb-4 text-base font-bold text-ink">{{ t('auth.profile.title') }}</h2>
    <ProfileForm />
  </section>
  <SecuritySettings />
</template>
