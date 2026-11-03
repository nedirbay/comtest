<script setup lang="ts">
import { AppButton, StatusPage } from '@comtest/ui'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'

const { t, locale } = useI18n({ useScope: 'global' })
</script>

<template>
  <StatusPage code="404" :title="t('pages.notFoundTitle')" :text="t('pages.notFoundText')">
    <RouterLink :to="`/${locale}`">
      <AppButton variant="primary">{{ t('common.home') }}</AppButton>
    </RouterLink>
  </StatusPage>
</template>
