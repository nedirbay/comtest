<script setup lang="ts">
/** Element Plus's button with our defaults. Other props and events pass straight through. */
withDefaults(
  defineProps<{
    variant?: 'primary' | 'secondary' | 'danger' | 'ghost'
    loading?: boolean
    block?: boolean
    type?: 'button' | 'submit'
  }>(),
  { variant: 'secondary', type: 'button' },
)
</script>

<template>
  <el-button
    :native-type="type"
    :type="variant === 'primary' ? 'primary' : variant === 'danger' ? 'danger' : undefined"
    :text="variant === 'ghost'"
    :loading="loading"
    :class="{ 'w-full': block }"
  >
    <slot />
  </el-button>
</template>
