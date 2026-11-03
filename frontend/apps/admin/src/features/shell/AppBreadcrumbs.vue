<script setup lang="ts">
import { computed, watchEffect } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { useShellStore } from './useShellStore'

const { t } = useI18n({ useScope: 'global' })
const route = useRoute()
const router = useRouter()
const shell = useShellStore()

interface Crumb {
  name: string
  label: string
}

const trail = computed<Crumb[]>(() => {
  const parents: Crumb[] = []
  let parent = route.meta.parent

  // Stops by itself at the top. The guard is for a typing mistake in the route table.
  for (let depth = 0; parent && depth < 5; depth += 1) {
    const meta = router.resolve({ name: parent, params: { locale: route.params.locale } }).meta

    parents.unshift({ name: parent, label: t(meta.title ?? parent) })
    parent = meta.parent
  }

  return route.name === 'dashboard'
    ? parents
    : [{ name: 'dashboard', label: t('admin.nav.dashboard') }, ...parents]
})

const currentLabel = computed(
  () => shell.pageTitle ?? (route.meta.title ? t(route.meta.title) : ''),
)

watchEffect(() => {
  document.title = currentLabel.value ? `${currentLabel.value} · ComTest` : 'ComTest'
})
</script>

<template>
  <el-breadcrumb v-if="trail.length" class="mb-4" separator="/" data-testid="breadcrumbs">
    <el-breadcrumb-item v-for="crumb in trail" :key="crumb.name" :to="{ name: crumb.name }">
      {{ crumb.label }}
    </el-breadcrumb-item>
    <el-breadcrumb-item>{{ currentLabel }}</el-breadcrumb-item>
  </el-breadcrumb>
</template>
