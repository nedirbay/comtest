<script setup lang="ts">
import { BrandMark, LocaleSwitcher, ThemeToggle } from '@comtest/ui'
import { useI18n } from 'vue-i18n'
import { RouterView, useRoute, useRouter } from 'vue-router'
import { theme } from '@/app/theme'

const { t, locale } = useI18n({ useScope: 'global' })
const route = useRoute()
const router = useRouter()

function changeLocale(next: string): void {
  void router.push({ params: { locale: next }, query: route.query })
}
</script>

<template>
  <div class="grid min-h-dvh lg:grid-cols-[minmax(0,5fr)_minmax(0,7fr)]">
    <aside
      class="relative hidden flex-col justify-between overflow-hidden bg-brand-900 p-12 text-white lg:flex"
    >
      <div class="relative z-10 flex items-center gap-3">
        <BrandMark :size="36" class="[--theme-primary:#fff] [--theme-on-primary:#0e3c2f]" />
        <span class="text-lg font-bold">ComTest</span>
      </div>
      <div class="relative z-10 max-w-sm">
        <p class="text-3xl leading-tight font-bold tracking-tight">{{ t('admin.brand') }}</p>
        <p class="mt-3 text-brand-200">{{ t('admin.tagline') }}</p>
      </div>
      <!-- Rows of güls, as on a carpet. Pure decoration, so screen readers skip it. -->
      <svg class="absolute inset-0 size-full opacity-[0.07]" aria-hidden="true">
        <defs>
          <pattern id="gul" width="96" height="96" patternUnits="userSpaceOnUse">
            <path
              d="M31 8h34l23 23v34L65 88H31L8 65V31z"
              fill="none"
              stroke="#fff"
              stroke-width="2"
            />
            <path d="M48 24 72 48 48 72 24 48z" fill="none" stroke="#fff" stroke-width="2" />
            <path d="M48 38 58 48 48 58 38 48z" fill="#fff" />
          </pattern>
        </defs>
        <rect width="100%" height="100%" fill="url(#gul)" />
      </svg>
    </aside>

    <div class="flex min-w-0 flex-col">
      <header class="flex items-center justify-between gap-2 p-4 lg:justify-end">
        <div class="flex items-center gap-2 lg:hidden">
          <BrandMark />
          <span class="font-bold text-ink">ComTest</span>
        </div>
        <div class="flex items-center gap-1">
          <LocaleSwitcher :model-value="locale" @update:model-value="changeLocale" />
          <ThemeToggle :is-dark="theme.isDark.value" @toggle="theme.toggle" />
        </div>
      </header>
      <main class="flex flex-1 items-start justify-center px-4 pt-4 pb-12 sm:items-center">
        <div class="w-full max-w-md">
          <RouterView />
        </div>
      </main>
    </div>
  </div>
</template>
