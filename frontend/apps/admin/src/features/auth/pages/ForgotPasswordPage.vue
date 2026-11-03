<script setup lang="ts">
import { ForgotPasswordForm } from '@comtest/auth'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'

const { t } = useI18n({ useScope: 'global' })
</script>

<template>
  <h1 class="mb-3 text-2xl font-bold tracking-tight text-ink">{{ t('auth.forgot.title') }}</h1>
  <ForgotPasswordForm />
  <p class="mt-5 text-center text-sm">
    <RouterLink :to="{ name: 'login' }" class="font-semibold text-primary hover:underline">
      {{ t('auth.forgot.backToLogin') }}
    </RouterLink>
  </p>
</template>
