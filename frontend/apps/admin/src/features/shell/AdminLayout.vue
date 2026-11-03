<script setup lang="ts">
import { useAuthStore } from '@comtest/auth'
import { BrandMark, ErrorBoundary, LocaleSwitcher, ThemeToggle } from '@comtest/ui'
import { House, Lock, Menu, SwitchButton, UserFilled } from '@element-plus/icons-vue'
import { computed, ref, watch, type Component } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink, RouterView, useRoute, useRouter } from 'vue-router'
import { theme } from '@/app/theme'

interface NavItem {
  name: string
  label: string
  icon: Component
  permission?: string
}

const { t, locale } = useI18n({ useScope: 'global' })
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const drawerOpen = ref(false)

const items = computed<NavItem[]>(() => [
  { name: 'dashboard', label: t('admin.nav.dashboard'), icon: House },
  { name: 'security', label: t('admin.nav.security'), icon: Lock },
])

const visibleItems = computed(() =>
  items.value.filter((item) => !item.permission || auth.can(item.permission)),
)

watch(
  () => route.fullPath,
  () => {
    drawerOpen.value = false
  },
)

function changeLocale(next: string): void {
  void router.push({ params: { locale: next }, query: route.query })
}

function onUserCommand(command: string): void {
  if (command === 'logout') {
    void auth.logout()
  } else {
    void router.push({ name: command })
  }
}
</script>

<template>
  <div class="min-h-dvh lg:grid lg:grid-cols-[15.5rem_minmax(0,1fr)]">
    <aside class="sticky top-0 hidden h-dvh flex-col border-r border-line bg-surface lg:flex">
      <div class="flex h-16 items-center gap-2.5 px-5">
        <BrandMark />
        <span class="font-bold text-ink">ComTest</span>
      </div>
      <nav class="flex-1 space-y-1 p-3" :aria-label="t('admin.nav.menu')">
        <RouterLink
          v-for="item in visibleItems"
          :key="item.name"
          :to="{ name: item.name }"
          class="flex items-center gap-3 rounded-control px-3 py-2.5 text-sm font-semibold text-ink-muted transition-colors hover:bg-primary-soft hover:text-ink"
          active-class="!bg-primary-soft !text-primary"
          exact-active-class="!bg-primary-soft !text-primary"
        >
          <el-icon :size="18"><component :is="item.icon" /></el-icon>
          {{ item.label }}
        </RouterLink>
      </nav>
    </aside>

    <div class="min-w-0">
      <header
        class="sticky top-0 z-20 flex h-16 items-center justify-between gap-3 border-b border-line bg-surface/90 px-4 backdrop-blur lg:px-8"
      >
        <button
          type="button"
          class="inline-flex size-10 items-center justify-center rounded-control text-ink-muted hover:bg-primary-soft lg:hidden"
          :aria-label="t('admin.nav.menu')"
          @click="drawerOpen = true"
        >
          <el-icon :size="20"><Menu /></el-icon>
        </button>
        <div class="flex-1" />
        <div class="flex items-center gap-1">
          <LocaleSwitcher :model-value="locale" @update:model-value="changeLocale" />
          <ThemeToggle :is-dark="theme.isDark.value" @toggle="theme.toggle" />
          <el-dropdown trigger="click" @command="onUserCommand">
            <button
              type="button"
              class="ml-1 inline-flex h-9 max-w-52 items-center gap-2 rounded-control px-2.5 text-sm font-semibold text-ink hover:bg-primary-soft"
              data-testid="user-menu"
            >
              <el-icon><UserFilled /></el-icon>
              <span class="truncate">{{ auth.profile?.fullName }}</span>
            </button>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item command="security" :icon="Lock">
                  {{ t('admin.nav.security') }}
                </el-dropdown-item>
                <el-dropdown-item
                  command="logout"
                  :icon="SwitchButton"
                  divided
                  data-testid="logout"
                >
                  {{ t('auth.logout') }}
                </el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </header>

      <main class="mx-auto max-w-6xl p-4 lg:p-8">
        <ErrorBoundary :reset-key="route.fullPath">
          <RouterView />
        </ErrorBoundary>
      </main>
    </div>

    <el-drawer v-model="drawerOpen" direction="ltr" size="17rem" :with-header="false">
      <nav class="space-y-1" :aria-label="t('admin.nav.menu')">
        <RouterLink
          v-for="item in visibleItems"
          :key="item.name"
          :to="{ name: item.name }"
          class="flex items-center gap-3 rounded-control px-3 py-2.5 text-sm font-semibold text-ink-muted"
          exact-active-class="!bg-primary-soft !text-primary"
        >
          <el-icon :size="18"><component :is="item.icon" /></el-icon>
          {{ item.label }}
        </RouterLink>
      </nav>
    </el-drawer>
  </div>
</template>
