<script setup lang="ts">
import { AppButton, StatusPage } from '@comtest/ui'
import { useI18n } from 'vue-i18n'

const { t, locale } = useI18n({ useScope: 'global' })

function reload(): void {
  window.location.assign(`/${locale.value}`)
}
</script>

<template>
  <StatusPage code="500" :title="t('pages.serverErrorTitle')" :text="t('pages.serverErrorText')">
    <AppButton variant="primary" @click="reload">{{ t('common.retry') }}</AppButton>
  </StatusPage>
</template>
