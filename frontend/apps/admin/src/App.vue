<script setup lang="ts">
import { elementPlusLocales, type AppLocale } from '@comtest/ui/locale'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterView } from 'vue-router'

const { locale } = useI18n({ useScope: 'global' })
const elementLocale = computed(() => elementPlusLocales[locale.value as AppLocale])
</script>

<template>
  <el-config-provider :locale="elementLocale">
    <RouterView />
  </el-config-provider>
</template>
