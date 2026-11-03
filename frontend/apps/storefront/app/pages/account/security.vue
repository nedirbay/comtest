<script setup lang="ts">
import { SecuritySettings } from '@comtest/auth'

definePageMeta({ middleware: 'auth', requiresAuth: true })

const { t } = useI18n()

useHead({ title: computed(() => t('auth.security.title')) })
</script>

<template>
  <AccountShell :title="t('auth.security.title')">
    <p class="mb-5 text-sm text-ink-muted">{{ t('auth.security.description') }}</p>
    <SecuritySettings class="max-w-3xl" />
  </AccountShell>
</template>
