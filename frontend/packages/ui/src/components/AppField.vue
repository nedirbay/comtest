<script setup lang="ts">
import { computed, inject } from 'vue'
import { FORM_CONTEXT } from '../composables/useForm'

const props = defineProps<{ name: string; label?: string; hint?: string }>()

const form = inject(FORM_CONTEXT, null)
const error = computed(() => form?.errors.value[props.name] ?? '')
</script>

<template>
  <el-form-item :label="label" :error="error">
    <slot />
    <p v-if="hint && !error" class="mt-1 text-xs leading-snug text-ink-subtle">{{ hint }}</p>
  </el-form-item>
</template>
