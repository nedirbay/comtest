<script setup lang="ts">
/** A six-digit code from an authenticator app or an SMS. */
const model = defineModel<string>({ required: true })

defineEmits<{ finish: [value: string] }>()

const isDigit = (char: string) => /^\d$/.test(char)
</script>

<template>
  <el-input-otp
    v-model="model"
    :length="6"
    inputmode="numeric"
    :validator="isDigit"
    @finish="$emit('finish', $event)"
  />
</template>
