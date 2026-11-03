<script setup lang="ts">
withDefaults(defineProps<{ count?: number }>(), { count: 8 })
</script>

<template>
  <ul class="grid grid-cols-2 gap-3 sm:gap-4 md:grid-cols-3 xl:grid-cols-4" aria-hidden="true">
    <li v-for="item in count" :key="item" class="rounded-card border border-line bg-surface p-3">
      <div class="aspect-square animate-shimmer rounded-control bg-line" />
      <div class="mt-3 h-4 w-4/5 animate-shimmer rounded bg-line" />
      <div class="mt-3 h-4 w-1/2 animate-shimmer rounded bg-line" />
    </li>
  </ul>
</template>
