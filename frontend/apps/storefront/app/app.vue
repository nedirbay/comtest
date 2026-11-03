<script setup lang="ts">
import { elementPlusLocales, type AppLocale } from '@comtest/ui/locale'

const { locale, localeProperties } = useI18n()

// hreflang and canonical links come with the SEO work (F13). They need each store's own address,
// and the i18n module's helper warns about that on every page, even when asked for language only.
useHead({ htmlAttrs: { lang: computed(() => localeProperties.value.language ?? locale.value) } })

const elementLocale = computed(() => elementPlusLocales[locale.value as AppLocale])
</script>

<template>
  <el-config-provider :locale="elementLocale">
    <NuxtLayout>
      <NuxtPage />
    </NuxtLayout>
  </el-config-provider>
</template>
