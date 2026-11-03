<script setup lang="ts">
import { onErrorCaptured, ref, watch } from 'vue'
import ErrorState from './ErrorState.vue'

const props = defineProps<{ resetKey?: unknown }>()
const emit = defineEmits<{ error: [error: unknown] }>()

const error = ref<unknown>(null)

// A failing widget should cost the user that widget, not the whole page.
onErrorCaptured((caught) => {
  error.value = caught
  emit('error', caught)

  return false
})

watch(
  () => props.resetKey,
  () => {
    error.value = null
  },
)
</script>

<template>
  <ErrorState v-if="error" :error="error" retryable @retry="error = null" />
  <slot v-else />
</template>
