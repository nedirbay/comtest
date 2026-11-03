<script setup lang="ts">
import { ArrowDown, Menu as MenuIcon } from '@element-plus/icons-vue'

const { t } = useI18n()
const localePath = useLocalePath()
const categories = useCategories()
</script>

<template>
  <!--
    Only in the browser: Element Plus's popover renders differently on the server, which breaks
    hydration of the whole page. The server's link leads to the same categories, and crawlers
    find them on the catalog page.
  -->
  <ClientOnly>
    <el-popover
      trigger="click"
      placement="bottom-start"
      :width="420"
      :show-arrow="false"
      :persistent="false"
    >
      <template #reference>
        <button
          type="button"
          class="inline-flex h-10 shrink-0 items-center gap-2 rounded-control bg-primary-soft px-3.5 text-sm font-semibold text-primary hover:bg-primary hover:text-on-primary"
          data-testid="category-menu"
        >
          <el-icon><MenuIcon /></el-icon>
          {{ t('store.nav.catalog') }}
          <el-icon :size="12"><ArrowDown /></el-icon>
        </button>
      </template>
      <nav :aria-label="t('store.nav.catalog')" class="max-h-[70vh] overflow-y-auto">
        <NuxtLink
          :to="localePath('/catalog')"
          class="block rounded-control px-2 py-1.5 text-sm font-semibold text-ink hover:bg-primary-soft"
        >
          {{ t('store.nav.allProducts') }}
        </NuxtLink>
        <div v-for="category in categories.data.value" :key="category.id" class="mt-2">
          <NuxtLink
            :to="localePath(`/catalog/${category.slug}`)"
            class="block rounded-control px-2 py-1.5 text-sm font-bold text-ink hover:bg-primary-soft"
          >
            {{ category.name }}
          </NuxtLink>
          <NuxtLink
            v-for="child in category.children"
            :key="child.id"
            :to="localePath(`/catalog/${child.slug}`)"
            class="block rounded-control px-2 py-1 pl-4 text-sm text-ink-muted hover:bg-primary-soft hover:text-ink"
          >
            {{ child.name }}
          </NuxtLink>
        </div>
      </nav>
    </el-popover>
    <template #fallback>
      <NuxtLink
        :to="localePath('/catalog')"
        class="inline-flex h-10 shrink-0 items-center gap-2 rounded-control bg-primary-soft px-3.5 text-sm font-semibold text-primary"
      >
        {{ t('store.nav.catalog') }}
      </NuxtLink>
    </template>
  </ClientOnly>
</template>
