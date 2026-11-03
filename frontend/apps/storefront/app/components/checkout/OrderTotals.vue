<script setup lang="ts">
import type { Schema } from '@comtest/api-client'
import { formatMoney } from '@comtest/ui'

/** Every figure here is what the server answered. Nothing is added up in the browser. */
withDefaults(defineProps<{ totals: Schema<'TotalsDto'>; shippingKnown?: boolean }>(), {
  shippingKnown: true,
})

const { t, locale } = useI18n()
</script>

<template>
  <dl class="space-y-2 text-sm">
    <div class="flex justify-between gap-4">
      <dt class="text-ink-muted">{{ t('store.totals.subtotal') }}</dt>
      <dd class="font-medium text-ink">
        {{ formatMoney({ amount: totals.subtotal, currency: totals.currency }, locale) }}
      </dd>
    </div>
    <div v-if="totals.discount > 0" class="flex justify-between gap-4">
      <dt class="text-ink-muted">{{ t('store.totals.discount') }}</dt>
      <dd class="font-medium text-sale">
        −{{ formatMoney({ amount: totals.discount, currency: totals.currency }, locale) }}
      </dd>
    </div>
    <div v-if="totals.tax > 0" class="flex justify-between gap-4">
      <dt class="text-ink-muted">{{ t('store.totals.tax') }}</dt>
      <dd class="font-medium text-ink">
        {{ formatMoney({ amount: totals.tax, currency: totals.currency }, locale) }}
      </dd>
    </div>
    <div class="flex justify-between gap-4">
      <dt class="text-ink-muted">{{ t('store.totals.shipping') }}</dt>
      <dd class="font-medium text-ink" data-testid="total-shipping">
        <template v-if="!shippingKnown">{{ t('store.totals.shippingLater') }}</template>
        <template v-else-if="totals.shipping === 0">{{ t('store.totals.free') }}</template>
        <template v-else>
          {{ formatMoney({ amount: totals.shipping, currency: totals.currency }, locale) }}
        </template>
      </dd>
    </div>
    <div class="flex items-baseline justify-between gap-4 border-t border-line pt-3">
      <dt class="font-bold text-ink">{{ t('store.totals.total') }}</dt>
      <dd class="text-xl font-bold text-ink" data-testid="total">
        {{ formatMoney({ amount: totals.total, currency: totals.currency }, locale) }}
      </dd>
    </div>
  </dl>
</template>
