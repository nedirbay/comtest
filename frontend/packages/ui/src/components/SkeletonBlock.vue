<script setup lang="ts">
/** A placeholder of a known size, so that the page does not jump when the data arrives. */
withDefaults(defineProps<{ lines?: number }>(), { lines: 3 })
</script>

<template>
  <div class="space-y-3" role="status" aria-busy="true">
    <div
      v-for="line in lines"
      :key="line"
      class="h-4 animate-shimmer rounded bg-line"
      :style="{ width: `${100 - ((line - 1) % 3) * 15}%` }"
    />
  </div>
</template>
