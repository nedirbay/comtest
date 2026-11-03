<script setup lang="ts">
import { House, Menu as MenuIcon, Search, ShoppingCart, User } from '@element-plus/icons-vue'

const { t } = useI18n()
const localePath = useLocalePath()
const { itemCount } = useCart()

const items = computed(() => [
  { to: localePath('/'), label: t('store.nav.home'), icon: House },
  { to: localePath('/catalog'), label: t('store.nav.catalog'), icon: MenuIcon },
  { to: localePath('/search'), label: t('store.search.title'), icon: Search },
  { to: localePath('/cart'), label: t('store.cart.title'), icon: ShoppingCart, badge: true },
  { to: localePath('/account'), label: t('store.nav.account'), icon: User },
])
</script>

<template>
  <nav
    class="fixed inset-x-0 bottom-0 z-30 border-t border-line bg-surface pb-[env(safe-area-inset-bottom)] lg:hidden"
    :aria-label="t('store.nav.menu')"
    data-testid="bottom-nav"
  >
    <ul class="grid grid-cols-5">
      <li v-for="item in items" :key="item.to">
        <NuxtLink
          :to="item.to"
          class="relative flex flex-col items-center gap-0.5 py-2 text-[0.6875rem] font-medium text-ink-muted"
          exact-active-class="!text-primary"
        >
          <el-icon :size="20"><component :is="item.icon" /></el-icon>
          <span class="max-w-full truncate px-1">{{ item.label }}</span>
          <span
            v-if="item.badge && itemCount > 0"
            class="absolute top-1 left-1/2 ml-1.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-sale px-1 text-[0.625rem] font-bold text-on-sale"
          >
            {{ itemCount }}
          </span>
        </NuxtLink>
      </li>
    </ul>
  </nav>
</template>
