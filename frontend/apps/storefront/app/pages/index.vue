<script setup lang="ts">
import { EmptyState } from '@comtest/ui'

const { t } = useI18n()
const store = useStoreInfo()

useSeoMeta({ description: computed(() => t('store.home.intro')) })
</script>

<template>
  <section
    v-if="store.data.value"
    class="relative overflow-hidden rounded-panel bg-brand-900 px-6 py-14 text-white sm:px-12 sm:py-20"
    data-testid="store-hero"
  >
    <h1 class="relative z-10 max-w-2xl text-3xl font-bold tracking-tight sm:text-display">
      {{ t('store.home.welcome', { name: store.data.value.name }) }}
    </h1>
    <p class="relative z-10 mt-3 max-w-xl text-brand-200">{{ t('store.home.intro') }}</p>
  </section>
  <EmptyState
    v-else-if="store.unavailable.value"
    :title="t('store.home.unavailableTitle')"
    :description="t('store.home.unavailableText')"
  />
</template>
