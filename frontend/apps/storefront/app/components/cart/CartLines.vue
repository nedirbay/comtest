<script setup lang="ts">
import { formatMoney, useApiErrorMessage } from '@comtest/ui'
import { Delete } from '@element-plus/icons-vue'

defineProps<{ compact?: boolean }>()

const { t, locale } = useI18n()
const localePath = useLocalePath()
const describeError = useApiErrorMessage()
const { cart, setQuantity, remove, drawerOpen } = useCart()

const money = (amount: number | null | undefined, currency: string | null | undefined) =>
  amount != null && currency ? formatMoney({ amount, currency }, locale.value) : ''

const optionText = (options: Record<string, unknown> | null | undefined) =>
  Object.values(options ?? {}).join(', ')

const mutationError = computed(() => setQuantity.error.value ?? remove.error.value)
const busy = computed(() => setQuantity.isPending.value || remove.isPending.value)
</script>

<template>
  <div>
    <el-alert
      v-if="mutationError"
      :title="describeError(mutationError)"
      type="error"
      show-icon
      :closable="false"
      class="mb-3"
    />
    <ul class="divide-y divide-line">
      <li
        v-for="line in cart?.lines"
        :key="line.variantId"
        class="py-4 first:pt-0"
        data-testid="cart-line"
      >
        <div class="flex flex-wrap items-start justify-between gap-x-4 gap-y-2">
          <div class="min-w-0 flex-1 basis-48">
            <NuxtLink
              v-if="line.productSlug"
              :to="localePath(`/products/${line.productSlug}`)"
              class="font-semibold text-ink hover:text-primary"
              @click="drawerOpen = false"
            >
              {{ line.productName }}
            </NuxtLink>
            <p v-else class="font-semibold text-ink-muted">{{ t('store.cart.gone') }}</p>
            <p v-if="optionText(line.options)" class="text-sm text-ink-muted">
              {{ optionText(line.options) }}
            </p>
            <p class="mt-0.5 text-sm text-ink-muted">{{ money(line.unitPrice, line.currency) }}</p>
          </div>
          <p class="font-bold text-ink" data-testid="line-total">
            {{ money(line.lineTotal, line.currency) }}
          </p>
        </div>

        <p
          v-if="!line.isAvailable"
          class="mt-2 text-sm font-medium text-danger"
          role="alert"
          data-testid="line-problem"
        >
          {{ t('store.cart.unavailable') }}
        </p>
        <p
          v-else-if="line.insufficientStock"
          class="mt-2 text-sm font-medium text-danger"
          role="alert"
          data-testid="line-problem"
        >
          {{ t('store.cart.notEnoughStock') }}
        </p>
        <p
          v-if="line.previousUnitPrice != null"
          class="mt-2 text-sm font-medium text-warning"
          role="status"
          data-testid="price-changed"
        >
          {{
            t('store.cart.priceChanged', {
              from: money(line.previousUnitPrice, line.currency),
              to: money(line.unitPrice, line.currency),
            })
          }}
        </p>

        <div class="mt-3 flex items-center justify-between gap-2">
          <el-input-number
            :model-value="line.quantity"
            :min="1"
            :max="999"
            :disabled="busy || !line.isAvailable"
            :size="compact ? 'small' : 'default'"
            :aria-label="t('store.cart.quantity')"
            @change="
              (value: number | undefined) =>
                value && setQuantity.mutate({ variantId: line.variantId, quantity: value })
            "
          />
          <el-button
            text
            :icon="Delete"
            :disabled="busy"
            :aria-label="t('store.cart.remove', { name: line.productName ?? '' })"
            data-testid="remove-line"
            @click="remove.mutate(line.variantId)"
          >
            <span v-if="!compact">{{ t('common.delete') }}</span>
          </el-button>
        </div>
      </li>
    </ul>
  </div>
</template>
