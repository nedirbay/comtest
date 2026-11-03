<script setup lang="ts">
import { useAuthStore } from '@comtest/auth'
import { AppButton } from '@comtest/ui'

defineProps<{ title: string }>()

const { t } = useI18n()
const localePath = useLocalePath()
const auth = useAuthStore()

const links = computed(() => [
  { to: localePath('/account'), label: t('store.account.profile'), exact: true },
  { to: localePath('/account/orders'), label: t('store.account.orders') },
  { to: localePath('/account/addresses'), label: t('store.account.addresses') },
  { to: localePath('/account/security'), label: t('store.account.security') },
])

async function signOut(): Promise<void> {
  await auth.logout()
  await navigateTo(localePath('/'))
}
</script>

<template>
  <div class="lg:grid lg:grid-cols-[14rem_minmax(0,1fr)] lg:gap-8">
    <aside class="mb-5 lg:mb-0">
      <nav
        :aria-label="t('store.account.title')"
        class="flex gap-1 overflow-x-auto lg:sticky lg:top-24 lg:flex-col"
      >
        <NuxtLink
          v-for="link in links"
          :key="link.to"
          :to="link.to"
          class="shrink-0 rounded-control px-3 py-2 text-sm font-semibold text-ink-muted hover:bg-primary-soft hover:text-ink"
          :active-class="link.exact ? undefined : '!bg-primary-soft !text-primary'"
          exact-active-class="!bg-primary-soft !text-primary"
        >
          {{ link.label }}
        </NuxtLink>
        <AppButton
          variant="ghost"
          class="!ml-0 shrink-0 lg:!justify-start"
          data-testid="logout"
          @click="signOut"
        >
          {{ t('auth.logout') }}
        </AppButton>
      </nav>
    </aside>
    <div class="min-w-0">
      <h1 class="mb-5 text-2xl font-bold tracking-tight text-ink">{{ title }}</h1>
      <slot />
    </div>
  </div>
</template>
