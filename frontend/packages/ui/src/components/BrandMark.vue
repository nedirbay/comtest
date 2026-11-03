<script setup lang="ts">
/** The mark is an octagonal gül, the medallion at the heart of Turkmen carpets. */
withDefaults(defineProps<{ size?: number }>(), { size: 32 })
</script>

<template>
  <svg :width="size" :height="size" viewBox="0 0 32 32" aria-hidden="true" class="shrink-0">
    <path d="M10.2 2h11.6L30 10.2v11.6L21.8 30H10.2L2 21.8V10.2z" class="fill-primary" />
    <path d="M16 6.5 25.5 16 16 25.5 6.5 16z" class="fill-on-primary" />
    <path d="M16 11.5 20.5 16 16 20.5 11.5 16z" class="fill-sale" />
  </svg>
</template>
