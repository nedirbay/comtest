<script setup lang="ts">
import {
  AppButton,
  EmptyState,
  ErrorState,
  PageHeader,
  SkeletonBlock,
  formatMoney,
} from '@comtest/ui'

const { t, locale } = useI18n()
const localePath = useLocalePath()
const { cart, isLoading, isError, error, refetch } = useCart()

useHead({ title: computed(() => t('store.cart.title')) })
</script>

<template>
  <PageHeader :title="t('store.cart.title')" />
  <ErrorState v-if="isError" :error="error" retryable @retry="refetch()" />
  <SkeletonBlock v-else-if="isLoading" :lines="6" class="max-w-3xl" />
  <EmptyState
    v-else-if="!cart?.lines.length"
    :title="t('store.cart.emptyTitle')"
    :description="t('store.cart.emptyText')"
  >
    <NuxtLink :to="localePath('/catalog')">
      <AppButton variant="primary">{{ t('store.home.browse') }}</AppButton>
    </NuxtLink>
  </EmptyState>
  <div v-else class="grid gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
    <section class="rounded-card border border-line bg-surface p-4 sm:p-5">
      <CartLines />
    </section>
    <aside>
      <div class="sticky top-24 rounded-card border border-line bg-surface p-5">
        <el-alert
          v-if="cart.hasProblems"
          :title="t('store.cart.hasProblems')"
          type="warning"
          show-icon
          :closable="false"
          class="mb-4"
          data-testid="cart-problems"
        />
        <div class="flex items-baseline justify-between text-ink">
          <span>{{ t('store.cart.subtotal') }}</span>
          <span class="text-xl font-bold" data-testid="cart-subtotal">
            {{ formatMoney({ amount: cart.subtotal, currency: cart.currency }, locale) }}
          </span>
        </div>
        <p class="mt-1 text-xs text-ink-subtle">{{ t('store.cart.shippingNote') }}</p>
        <NuxtLink :to="cart.hasProblems ? undefined : localePath('/checkout')" class="mt-4 block">
          <AppButton
            variant="primary"
            size="large"
            block
            :disabled="cart.hasProblems"
            data-testid="go-checkout"
          >
            {{ t('store.cart.checkout') }}
          </AppButton>
        </NuxtLink>
      </div>
    </aside>
  </div>
</template>
