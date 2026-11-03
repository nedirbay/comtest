<script setup lang="ts">
import { useAuthStore } from '@comtest/auth'
import { BrandMark, ErrorBoundary, LocaleSwitcher, ThemeToggle } from '@comtest/ui'
import { Lock, Menu, Search, SwitchButton, UserFilled } from '@element-plus/icons-vue'
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterView, useRoute, useRouter } from 'vue-router'
import { theme } from '@/app/theme'
import ConfirmHost from '@/shared/components/ConfirmHost.vue'
import AdminNav from './AdminNav.vue'
import AppBreadcrumbs from './AppBreadcrumbs.vue'
import CommandPalette from './CommandPalette.vue'
import IdleGuard from './IdleGuard.vue'
import OrderNotifier from './OrderNotifier.vue'
import ShortcutsHelp from './ShortcutsHelp.vue'
import StoreSwitcher from './StoreSwitcher.vue'
import { useShortcuts } from './useShortcuts'

const { t, locale } = useI18n({ useScope: 'global' })
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const drawerOpen = ref(false)
const paletteOpen = ref(false)
const helpOpen = ref(false)

useShortcuts(paletteOpen, helpOpen)

watch(
  () => route.fullPath,
  () => {
    drawerOpen.value = false
  },
)

// Reused for a change of record, so that moving between two products gives a clean form. A change
// of language keeps the page, and with it whatever is typed into its forms.
const pageKey = computed(() => `${String(route.name)}:${String(route.params.id ?? '')}`)

// The boundary shows a friendly message, and without this the cause would be lost.
function reportPageError(error: unknown): void {
  console.error(error)
}

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
      <div class="flex h-16 shrink-0 items-center gap-2.5 px-5">
        <BrandMark />
        <span class="font-bold text-ink">ComTest</span>
      </div>
      <div class="shrink-0 px-3 pb-3"><StoreSwitcher /></div>
      <div class="min-h-0 flex-1 overflow-y-auto px-3 pb-4"><AdminNav /></div>
    </aside>

    <div class="min-w-0">
      <header
        class="sticky top-0 z-20 flex h-16 items-center gap-1 border-b border-line bg-surface/90 px-2 backdrop-blur sm:gap-3 sm:px-4 lg:px-8"
      >
        <button
          type="button"
          class="inline-flex size-10 shrink-0 items-center justify-center rounded-control text-ink-muted hover:bg-primary-soft lg:hidden"
          :aria-label="t('admin.nav.menu')"
          data-testid="menu-button"
          @click="drawerOpen = true"
        >
          <el-icon :size="20"><Menu /></el-icon>
        </button>
        <button
          type="button"
          class="inline-flex size-10 shrink-0 items-center justify-center gap-2 rounded-control text-sm text-ink-muted hover:bg-primary-soft sm:h-10 sm:w-64 sm:justify-start sm:border sm:border-line sm:px-3"
          data-testid="palette-button"
          @click="paletteOpen = true"
        >
          <el-icon :size="18" class="shrink-0"><Search /></el-icon>
          <!-- The name comes from the text, so a phone, which hides it, still gets one to read out. -->
          <span class="sr-only sm:hidden">{{ t('admin.palette.title') }}</span>
          <span class="hidden flex-1 truncate text-left sm:inline">
            {{ t('admin.palette.button') }}
          </span>
          <kbd
            class="hidden rounded border border-line-strong px-1.5 text-xs font-semibold sm:inline"
          >
            Ctrl K
          </kbd>
        </button>
        <div class="flex-1" />
        <div class="flex items-center gap-0.5 sm:gap-1">
          <OrderNotifier />
          <LocaleSwitcher :model-value="locale" @update:model-value="changeLocale" />
          <ThemeToggle :is-dark="theme.isDark.value" @toggle="theme.toggle" />
          <el-dropdown trigger="click" @command="onUserCommand">
            <button
              type="button"
              class="inline-flex h-9 max-w-52 items-center gap-2 rounded-control px-2 text-sm font-semibold text-ink hover:bg-primary-soft"
              :aria-label="auth.profile?.fullName"
              data-testid="user-menu"
            >
              <el-icon><UserFilled /></el-icon>
              <span class="hidden truncate sm:inline">{{ auth.profile?.fullName }}</span>
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

      <main class="mx-auto max-w-7xl p-4 lg:p-8">
        <AppBreadcrumbs />
        <ErrorBoundary :reset-key="route.fullPath" @error="reportPageError">
          <RouterView v-slot="{ Component }">
            <component :is="Component" :key="pageKey" />
          </RouterView>
        </ErrorBoundary>
      </main>
    </div>

    <el-drawer v-model="drawerOpen" direction="ltr" size="17.5rem" :with-header="false">
      <div class="mb-4 flex items-center gap-2.5 px-1">
        <BrandMark />
        <span class="font-bold text-ink">ComTest</span>
      </div>
      <div class="mb-4"><StoreSwitcher /></div>
      <!-- A click closes it even if the unsaved-changes question then keeps the user on the page. -->
      <AdminNav @navigate="drawerOpen = false" />
    </el-drawer>

    <CommandPalette v-model="paletteOpen" />
    <ShortcutsHelp v-model="helpOpen" />
    <ConfirmHost />
    <IdleGuard />
  </div>
</template>
