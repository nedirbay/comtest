<script setup lang="ts">
import { useAuthStore } from '@comtest/auth'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink, useRoute } from 'vue-router'
import { NAV_SECTIONS, visibleSections } from '@/app/navigation'

defineEmits<{ navigate: [] }>()

const { t } = useI18n({ useScope: 'global' })
const auth = useAuthStore()
const route = useRoute()

const sections = computed(() => visibleSections((permission) => auth.can(permission)))
const allNames = NAV_SECTIONS.flatMap((section) => section.items.map((item) => item.name))

// A detail page has no menu item of its own, so the list it belongs to stays marked.
const current = computed(() =>
  allNames.includes(String(route.name)) ? route.name : route.meta.parent,
)
</script>

<template>
  <nav class="space-y-5" :aria-label="t('admin.nav.menu')" data-testid="side-menu">
    <div v-for="section in sections" :key="section.labelKey">
      <p class="mb-1 px-3 text-xs font-semibold uppercase tracking-wide text-ink-subtle">
        {{ t(section.labelKey) }}
      </p>
      <RouterLink
        v-for="item in section.items"
        :key="item.name"
        :to="{ name: item.name }"
        class="flex items-center gap-3 rounded-control px-3 py-2 text-sm font-semibold text-ink-muted transition-colors hover:bg-primary-soft hover:text-ink"
        :class="{ '!bg-primary-soft !text-primary': current === item.name }"
        :aria-current="current === item.name ? 'page' : undefined"
        :data-testid="`nav-${item.name}`"
        @click="$emit('navigate')"
      >
        <el-icon :size="18"><component :is="item.icon" /></el-icon>
        {{ t(item.labelKey) }}
      </RouterLink>
    </div>
  </nav>
</template>
