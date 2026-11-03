<script setup lang="ts">
import { AppButton, EmptyState, formatMoney } from '@comtest/ui'

const { t, locale } = useI18n()
const localePath = useLocalePath()
const { cart, drawerOpen } = useCart()

const isEmpty = computed(() => !cart.value || cart.value.lines.length === 0)

function go(path: string): void {
  drawerOpen.value = false
  void navigateTo(localePath(path))
}
</script>

<template>
  <el-drawer
    v-model="drawerOpen"
    :title="t('store.cart.title')"
    size="min(26rem, 100vw)"
    data-testid="cart-drawer"
  >
    <EmptyState
      v-if="isEmpty"
      :title="t('store.cart.emptyTitle')"
      :description="t('store.cart.emptyText')"
    />
    <CartLines v-else compact />
    <template v-if="!isEmpty" #footer>
      <div class="space-y-3">
        <div class="flex items-baseline justify-between text-ink">
          <span class="text-sm">{{ t('store.cart.subtotal') }}</span>
          <span class="text-lg font-bold">
            {{ formatMoney({ amount: cart!.subtotal, currency: cart!.currency }, locale) }}
          </span>
        </div>
        <AppButton
          variant="primary"
          block
          :disabled="cart!.hasProblems"
          data-testid="drawer-checkout"
          @click="go('/checkout')"
        >
          {{ t('store.cart.checkout') }}
        </AppButton>
        <AppButton block data-testid="drawer-cart" class="!ml-0" @click="go('/cart')">
          {{ t('store.cart.open') }}
        </AppButton>
      </div>
    </template>
  </el-drawer>
</template>
