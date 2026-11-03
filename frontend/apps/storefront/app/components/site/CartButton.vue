<script setup lang="ts">
import { ShoppingCart } from '@element-plus/icons-vue'

const { t } = useI18n()
const { itemCount, drawerOpen } = useCart()
</script>

<template>
  <button
    type="button"
    class="relative inline-flex size-10 items-center justify-center rounded-control text-ink hover:bg-primary-soft"
    :aria-label="t('store.cart.openWithCount', { count: itemCount })"
    data-testid="cart-button"
    @click="drawerOpen = true"
  >
    <el-icon :size="22"><ShoppingCart /></el-icon>
    <span
      v-if="itemCount > 0"
      class="absolute -top-0.5 -right-0.5 flex h-5 min-w-5 items-center justify-center rounded-full bg-sale px-1 text-xs font-bold text-on-sale"
      data-testid="cart-count"
    >
      {{ itemCount }}
    </span>
  </button>
</template>
