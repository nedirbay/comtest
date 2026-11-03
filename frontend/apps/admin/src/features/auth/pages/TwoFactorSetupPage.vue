<script setup lang="ts">
import { TwoFactorSetupPanel } from '@comtest/auth'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'

const { t, locale } = useI18n({ useScope: 'global' })
const router = useRouter()
</script>

<template>
  <h1 class="mb-2 text-2xl font-bold tracking-tight text-ink">{{ t('auth.setup.title') }}</h1>
  <p class="mb-6 text-sm text-ink-muted">{{ t('auth.setup.required') }}</p>
  <TwoFactorSetupPanel @done="router.replace(`/${locale}`)" />
</template>
