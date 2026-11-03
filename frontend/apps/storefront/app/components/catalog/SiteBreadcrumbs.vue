<script setup lang="ts">
export interface Crumb {
  label: string
  to?: string
}

defineProps<{ items: Crumb[] }>()

const { t } = useI18n()
const localePath = useLocalePath()
</script>

<template>
  <nav :aria-label="t('store.nav.breadcrumbs')" class="mb-4 text-sm text-ink-muted">
    <ol class="flex flex-wrap items-center gap-1.5">
      <li>
        <NuxtLink :to="localePath('/')" class="hover:text-primary">
          {{ t('store.nav.home') }}
        </NuxtLink>
      </li>
      <li v-for="item in items" :key="item.label" class="flex items-center gap-1.5">
        <span aria-hidden="true">/</span>
        <NuxtLink v-if="item.to" :to="item.to" class="hover:text-primary">
          {{ item.label }}
        </NuxtLink>
        <span v-else aria-current="page" class="font-medium text-ink">{{ item.label }}</span>
      </li>
    </ol>
  </nav>
</template>
