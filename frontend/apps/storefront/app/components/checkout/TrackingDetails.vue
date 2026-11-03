<script setup lang="ts">
import type { Schema } from '@comtest/api-client'
import { StatusTag, formatDateTime, formatMoney, safeUrl } from '@comtest/ui'

const props = defineProps<{ order: Schema<'OrderTrackingDto'> | Schema<'OrderDto'> }>()

const { t, locale } = useI18n()

const trackingLink = computed(() => safeUrl(props.order.tracking.url))

const optionText = (options: Record<string, unknown> | null | undefined) =>
  Object.values(options ?? {}).join(', ')
</script>

<template>
  <div class="space-y-5">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div>
        <p class="text-sm text-ink-muted">{{ t('store.orders.number') }}</p>
        <p class="text-xl font-bold text-ink" data-testid="order-number">{{ order.number }}</p>
        <p class="text-xs text-ink-subtle">{{ formatDateTime(order.placedAt, locale) }}</p>
      </div>
      <div class="flex flex-wrap gap-2">
        <span data-testid="order-status">
          <StatusTag kind="orderStatus" :value="order.status" />
        </span>
        <StatusTag kind="paymentState" :value="order.paymentState" />
      </div>
    </div>

    <dl class="grid gap-3 text-sm sm:grid-cols-2">
      <div>
        <dt class="text-ink-subtle">{{ t('store.checkout.steps.shipping') }}</dt>
        <dd class="font-medium text-ink">{{ order.shippingMethodName }}</dd>
      </div>
      <div>
        <dt class="text-ink-subtle">{{ t('store.checkout.steps.payment') }}</dt>
        <dd class="font-medium text-ink">
          <StatusTag kind="paymentMethod" :value="order.paymentMethod" />
        </dd>
      </div>
      <div v-if="order.tracking.carrier || order.tracking.number">
        <dt class="text-ink-subtle">{{ t('store.orders.tracking') }}</dt>
        <dd class="font-medium text-ink">
          {{ order.tracking.carrier }} {{ order.tracking.number }}
          <a
            v-if="trackingLink"
            :href="trackingLink"
            target="_blank"
            rel="noopener noreferrer"
            class="ml-1 text-primary underline"
          >
            {{ t('store.orders.trackingLink') }}
          </a>
        </dd>
      </div>
    </dl>

    <ul class="divide-y divide-line rounded-control border border-line">
      <li
        v-for="(item, index) in order.items"
        :key="index"
        class="flex justify-between gap-4 p-3 text-sm"
        data-testid="order-item"
      >
        <span class="min-w-0">
          <span class="font-medium text-ink">{{ item.productName }}</span>
          <span v-if="optionText(item.options)" class="block text-xs text-ink-muted">
            {{ optionText(item.options) }}
          </span>
          <span class="block text-xs text-ink-muted">
            {{ item.quantity }} ×
            {{ formatMoney({ amount: item.unitPrice, currency: order.totals.currency }, locale) }}
          </span>
        </span>
        <span class="shrink-0 font-semibold text-ink">
          {{ formatMoney({ amount: item.lineTotal, currency: order.totals.currency }, locale) }}
        </span>
      </li>
    </ul>

    <OrderTotals :totals="order.totals" class="ml-auto max-w-sm" />
  </div>
</template>
