<script setup lang="ts">
defineProps<{ code: number | string; title: string; text: string }>()
</script>

<template>
  <main class="flex min-h-[70vh] flex-col items-center justify-center gap-3 px-6 py-16 text-center">
    <p class="text-6xl font-extrabold tracking-tight text-primary">{{ code }}</p>
    <h1 class="text-2xl font-bold text-ink">{{ title }}</h1>
    <p class="max-w-md text-ink-muted">{{ text }}</p>
    <div class="mt-4 flex flex-wrap justify-center gap-2"><slot /></div>
  </main>
</template>
