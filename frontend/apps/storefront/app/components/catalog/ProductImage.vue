<script setup lang="ts">
import { safeUrl } from '@comtest/ui'

const props = defineProps<{
  src?: string | null
  alt: string
  eager?: boolean
  srcset?: string
  sizes?: string
}>()

const url = computed(() => safeUrl(props.src))
</script>

<template>
  <div class="relative aspect-square overflow-hidden rounded-control bg-canvas">
    <img
      v-if="url"
      :src="url"
      :srcset="srcset"
      :sizes="sizes"
      :alt="alt"
      width="600"
      height="600"
      :loading="eager ? 'eager' : 'lazy'"
      :fetchpriority="eager ? 'high' : undefined"
      decoding="async"
      class="size-full object-contain"
    />
    <!-- A product without a photo keeps its place in the grid, and gets the store's mark. -->
    <div
      v-else
      class="flex size-full items-center justify-center text-line-strong"
      role="img"
      :aria-label="alt"
    >
      <svg viewBox="0 0 96 96" class="size-1/3" aria-hidden="true">
        <path
          d="M31 8h34l23 23v34L65 88H31L8 65V31z"
          fill="none"
          stroke="currentColor"
          stroke-width="3"
        />
        <path d="M48 24 72 48 48 72 24 48z" fill="none" stroke="currentColor" stroke-width="3" />
        <path d="M48 38 58 48 48 58 38 48z" fill="currentColor" />
      </svg>
    </div>
  </div>
</template>
