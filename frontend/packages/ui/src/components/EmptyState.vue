<script setup lang="ts">
import { useI18n } from 'vue-i18n'

defineProps<{ title?: string; description?: string }>()

const { t } = useI18n({ useScope: 'global' })
</script>

<template>
  <div class="flex flex-col items-center gap-2 px-6 py-12 text-center">
    <p class="text-base font-semibold text-ink">{{ title ?? t('states.emptyTitle') }}</p>
    <p v-if="description" class="max-w-md text-sm text-ink-muted">{{ description }}</p>
    <div v-if="$slots.default" class="mt-3"><slot /></div>
  </div>
</template>
