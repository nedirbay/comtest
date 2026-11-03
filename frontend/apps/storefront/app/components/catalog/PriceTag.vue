<script setup lang="ts">
import { formatMoney } from '@comtest/ui'

defineProps<{
  amount: number | null | undefined
  currency: string | null | undefined
  compareAt?: number | null
  size?: 'normal' | 'large'
}>()

const { locale } = useI18n()
</script>

<template>
  <p v-if="amount != null && currency" class="flex flex-wrap items-baseline gap-x-2">
    <span
      class="font-bold"
      :class="[
        size === 'large' ? 'text-3xl tracking-tight' : 'text-base',
        compareAt ? 'text-sale' : 'text-ink',
      ]"
      data-testid="price"
    >
      {{ formatMoney({ amount, currency }, locale) }}
    </span>
    <s v-if="compareAt" class="text-sm text-ink-subtle">
      {{ formatMoney({ amount: compareAt, currency }, locale) }}
    </s>
  </p>
</template>
