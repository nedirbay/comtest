<script setup lang="ts">
defineProps<{ title: string; description?: string }>()
</script>

<template>
  <header class="mb-6 flex flex-wrap items-end justify-between gap-4">
    <div class="min-w-0">
      <slot name="breadcrumbs" />
      <h1 class="text-2xl font-bold tracking-tight text-ink">{{ title }}</h1>
      <p v-if="description" class="mt-1 max-w-2xl text-sm text-ink-muted">{{ description }}</p>
    </div>
    <div v-if="$slots.actions" class="flex flex-wrap items-center gap-2">
      <slot name="actions" />
    </div>
  </header>
</template>
