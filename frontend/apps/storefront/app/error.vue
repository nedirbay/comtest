<script setup lang="ts">
import { AppButton, StatusPage } from '@comtest/ui'
import type { NuxtError } from '#app'

const props = defineProps<{ error: NuxtError }>()

const { t, locale, localeProperties } = useI18n()
const localePath = useLocalePath()

const kind = computed(() => {
  if (props.error.status === 404) {
    return 'notFound'
  }

  return props.error.status === 403 ? 'forbidden' : 'serverError'
})

useSiteTheme()
useHead({
  title: computed(() => t(`pages.${kind.value}Title`)),
  // The error page replaces app.vue, which sets the language on normal pages.
  htmlAttrs: { lang: computed(() => localeProperties.value.language ?? locale.value) },
})
</script>

<template>
  <StatusPage
    :code="error.status ?? 500"
    :title="t(`pages.${kind}Title`)"
    :text="t(`pages.${kind}Text`)"
  >
    <AppButton variant="primary" @click="clearError({ redirect: localePath('/') })">
      {{ t('common.home') }}
    </AppButton>
  </StatusPage>
</template>
