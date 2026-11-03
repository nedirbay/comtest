<script setup lang="ts">
import { useAuthStore } from '@comtest/auth'
import { PageHeader } from '@comtest/ui'
import { useI18n } from 'vue-i18n'

const { t } = useI18n({ useScope: 'global' })
const auth = useAuthStore()
</script>

<template>
  <PageHeader
    :title="t('admin.dashboard.greeting', { name: auth.profile?.fullName ?? '' })"
    :description="t('admin.dashboard.intro')"
  />
</template>
