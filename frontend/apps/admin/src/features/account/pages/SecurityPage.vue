<script setup lang="ts">
import { SecuritySettings } from '@comtest/auth'
import { PageHeader } from '@comtest/ui'
import { useI18n } from 'vue-i18n'

const { t } = useI18n({ useScope: 'global' })
</script>

<template>
  <PageHeader :title="t('auth.security.title')" :description="t('auth.security.description')" />
  <SecuritySettings />
</template>
