<script setup lang="ts">
import { SecuritySettings } from '@comtest/auth'
import { PageHeader } from '@comtest/ui'

definePageMeta({ middleware: 'auth', requiresAuth: true })

const { t } = useI18n()

useHead({ title: computed(() => t('auth.security.title')) })
</script>

<template>
  <PageHeader :title="t('auth.security.title')" :description="t('auth.security.description')" />
  <SecuritySettings class="max-w-3xl" />
</template>
