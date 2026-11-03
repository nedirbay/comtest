<script setup lang="ts">
import { BrandMark, LocaleSwitcher, ThemeToggle } from '@comtest/ui'

const { locale } = useI18n()
const localePath = useLocalePath()
const switchLocalePath = useSwitchLocalePath()
const store = useStoreInfo()
const theme = useSiteTheme()

const storeName = computed(() => store.data.value?.name ?? 'ComTest')

useHead({ titleTemplate: (title) => (title ? `${title} · ${storeName.value}` : storeName.value) })

function changeLocale(next: string): void {
  void navigateTo(switchLocalePath(next as 'tk' | 'ru' | 'en'))
}
</script>

<template>
  <div class="flex min-h-dvh flex-col">
    <header class="flex items-center justify-between gap-2 p-4">
      <NuxtLink :to="localePath('/')" class="flex min-w-0 items-center gap-2.5">
        <BrandMark />
        <span class="truncate font-bold text-ink">{{ storeName }}</span>
      </NuxtLink>
      <div class="flex items-center gap-1">
        <LocaleSwitcher :model-value="locale" @update:model-value="changeLocale" />
        <ThemeToggle :is-dark="theme.isDark.value" @toggle="theme.toggle" />
      </div>
    </header>
    <main class="flex flex-1 items-start justify-center px-4 pt-4 pb-12 sm:items-center">
      <div
        class="w-full max-w-md rounded-panel border border-line bg-surface p-6 shadow-card sm:p-8"
      >
        <slot />
      </div>
    </main>
  </div>
</template>
