<script setup lang="ts">
defineProps<{ error?: string | null }>()
defineEmits<{ submit: [] }>()
</script>

<template>
  <el-form label-position="top" novalidate @submit.prevent="$emit('submit')">
    <el-alert
      v-if="error"
      :title="error"
      type="error"
      show-icon
      :closable="false"
      class="mb-4"
      role="alert"
    />
    <slot />
  </el-form>
</template>
