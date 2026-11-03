<script setup lang="ts">
import { useAuthStore } from '@comtest/auth'
import { BrandMark, LocaleSwitcher, ThemeToggle, formatPhone, safeUrl } from '@comtest/ui'
import { UserFilled } from '@element-plus/icons-vue'

const { t, locale } = useI18n()
const localePath = useLocalePath()
const switchLocalePath = useSwitchLocalePath()
const auth = useAuthStore()
const store = useStoreInfo()
const theme = useSiteTheme()

const { drawerOpen } = useCart()
const cartOpenedOnce = ref(false)

watch(drawerOpen, (open) => {
  cartOpenedOnce.value ||= open
})

const storeName = computed(() => store.data.value?.name ?? 'ComTest')
const logo = computed(() => safeUrl(store.data.value?.logoUrl))

useHead({ titleTemplate: (title) => (title ? `${title} · ${storeName.value}` : storeName.value) })
useSeoMeta({ ogSiteName: () => storeName.value })
useLanguageLinks()

function changeLocale(next: string): void {
  void navigateTo(switchLocalePath(next as 'tk' | 'ru' | 'en'))
}
</script>

<template>
  <div class="flex min-h-dvh flex-col pb-16 lg:pb-0">
    <a
      href="#main"
      class="sr-only focus:not-sr-only focus:absolute focus:top-2 focus:left-2 focus:z-50 focus:rounded-control focus:bg-surface focus:px-3 focus:py-2 focus:shadow-raised"
    >
      {{ t('store.nav.skip') }}
    </a>
    <header class="sticky top-0 z-20 border-b border-line bg-surface/95 backdrop-blur">
      <div class="mx-auto flex h-16 max-w-7xl items-center gap-3 px-4 lg:px-6">
        <NuxtLink
          :to="localePath('/')"
          class="flex min-w-0 shrink items-center gap-2.5 lg:shrink-0"
        >
          <img
            v-if="logo"
            :src="logo"
            alt=""
            width="32"
            height="32"
            class="size-8 rounded object-contain"
          />
          <BrandMark v-else />
          <span class="truncate font-bold text-ink">{{ storeName }}</span>
        </NuxtLink>

        <div class="hidden flex-1 items-center gap-2 lg:flex">
          <CategoryMenu />
          <SiteSearch class="max-w-xl" />
        </div>

        <div class="ml-auto flex shrink-0 items-center gap-0.5">
          <LocaleSwitcher :model-value="locale" @update:model-value="changeLocale" />
          <ThemeToggle :is-dark="theme.isDark.value" @toggle="theme.toggle" />
          <NuxtLink
            v-if="auth.isAuthenticated"
            :to="localePath('/account')"
            class="ml-1 hidden h-9 max-w-44 items-center gap-2 rounded-control px-2.5 text-sm font-semibold text-ink hover:bg-primary-soft lg:inline-flex"
            data-testid="account-link"
          >
            <el-icon><UserFilled /></el-icon>
            <span class="truncate">{{ auth.profile?.fullName ?? t('store.nav.account') }}</span>
          </NuxtLink>
          <NuxtLink
            v-else-if="auth.status !== 'unknown'"
            :to="localePath('/login')"
            class="ml-1 hidden h-9 items-center rounded-control bg-primary px-3.5 text-sm font-semibold text-on-primary hover:bg-primary-hover lg:inline-flex"
          >
            {{ t('store.nav.login') }}
          </NuxtLink>
          <div v-else class="ml-1 hidden h-9 w-20 lg:block" aria-hidden="true" />
          <CartButton />
        </div>
      </div>
    </header>

    <main id="main" class="mx-auto w-full max-w-7xl flex-1 px-4 py-6 lg:px-6 lg:py-8">
      <slot />
    </main>

    <footer class="border-t border-line bg-surface">
      <div class="mx-auto flex max-w-7xl flex-wrap justify-between gap-6 px-4 py-8 text-sm lg:px-6">
        <div>
          <p class="font-bold text-ink">{{ storeName }}</p>
          <p class="mt-1 text-ink-subtle">© {{ t('store.footer.rights') }}</p>
          <NuxtLink
            :to="localePath('/track')"
            class="mt-2 inline-block text-ink-muted hover:text-primary"
          >
            {{ t('store.track.title') }}
          </NuxtLink>
        </div>
        <dl v-if="store.data.value" class="flex flex-wrap gap-x-10 gap-y-3 text-ink-muted">
          <div v-if="store.data.value.contactPhone || store.data.value.contactEmail">
            <dt class="font-semibold text-ink">{{ t('store.footer.contacts') }}</dt>
            <dd v-if="store.data.value.contactPhone">
              <a :href="`tel:${store.data.value.contactPhone}`" class="hover:text-primary">
                {{ formatPhone(store.data.value.contactPhone) }}
              </a>
            </dd>
            <dd v-if="store.data.value.contactEmail">
              <a :href="`mailto:${store.data.value.contactEmail}`" class="hover:text-primary">
                {{ store.data.value.contactEmail }}
              </a>
            </dd>
          </div>
          <div v-if="store.data.value.address">
            <dt class="font-semibold text-ink">{{ t('store.footer.address') }}</dt>
            <dd>{{ store.data.value.address }}</dd>
          </div>
          <div v-if="store.data.value.workingHours">
            <dt class="font-semibold text-ink">{{ t('store.footer.hours') }}</dt>
            <dd>{{ store.data.value.workingHours }}</dd>
          </div>
        </dl>
      </div>
    </footer>

    <ClientOnly>
      <BottomNav />
      <!-- Its code loads with the first opening: most visitors look around before buying. -->
      <LazyCartDrawer v-if="cartOpenedOnce" />
    </ClientOnly>
  </div>
</template>
