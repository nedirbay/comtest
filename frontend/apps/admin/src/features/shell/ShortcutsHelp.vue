<script setup lang="ts">
import { AppDialog } from '@comtest/ui'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

const open = defineModel<boolean>({ required: true })
const { t } = useI18n({ useScope: 'global' })

const rows = computed(() => [
  { keys: ['Ctrl/⌘', 'K'], label: t('admin.shortcuts.search') },
  { keys: ['/'], label: t('admin.shortcuts.search') },
  { keys: ['g', 'd'], label: t('admin.nav.dashboard') },
  { keys: ['g', 'o'], label: t('admin.nav.orders') },
  { keys: ['g', 'p'], label: t('admin.nav.products') },
  { keys: ['g', 'c'], label: t('admin.nav.customers') },
  { keys: ['g', 's'], label: t('admin.nav.stock') },
  { keys: ['?'], label: t('admin.shortcuts.help') },
])
</script>

<template>
  <AppDialog v-model="open" :title="t('admin.shortcuts.title')">
    <dl class="space-y-2 text-sm" data-testid="shortcuts-help">
      <div v-for="row in rows" :key="row.keys.join()" class="flex items-center justify-between">
        <dt class="text-ink-muted">{{ row.label }}</dt>
        <dd class="flex gap-1">
          <kbd
            v-for="key in row.keys"
            :key="key"
            class="rounded border border-line-strong px-1.5 py-0.5 text-xs font-semibold text-ink"
          >
            {{ key }}
          </kbd>
        </dd>
      </div>
    </dl>
    <template #actions>
      <el-button @click="open = false">{{ t('common.close') }}</el-button>
    </template>
  </AppDialog>
</template>
