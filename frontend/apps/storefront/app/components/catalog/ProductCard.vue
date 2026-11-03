<script setup lang="ts">
import type { Schema } from '@comtest/api-client'

defineProps<{ product: Schema<'StoreProductListItemDto'> }>()

const { t } = useI18n()
const localePath = useLocalePath()
</script>

<template>
  <article
    class="group relative flex flex-col rounded-card border border-line bg-surface p-3 transition-shadow hover:shadow-raised"
    data-testid="product-card"
  >
    <div class="relative">
      <ProductImage :src="product.imageUrl" :alt="product.name" />
      <span
        v-if="product.compareAtAmount"
        class="absolute top-2 left-2 rounded-md bg-sale px-2 py-0.5 text-xs font-bold text-on-sale"
      >
        {{ t('store.product.sale') }}
      </span>
    </div>
    <h3 class="mt-3 line-clamp-2 min-h-10 text-sm font-semibold text-ink">
      <!-- The whole card is the link, while the accessible name stays the product's name. -->
      <NuxtLink
        :to="localePath(`/products/${product.slug}`)"
        class="after:absolute after:inset-0 after:rounded-card focus-visible:outline-none focus-visible:after:outline-2 focus-visible:after:outline-offset-2 focus-visible:after:outline-primary"
      >
        {{ product.name }}
      </NuxtLink>
    </h3>
    <PriceTag
      class="mt-2"
      :amount="product.minPrice"
      :currency="product.currency"
      :compare-at="product.compareAtAmount"
    />
    <p
      class="mt-1 text-xs font-medium"
      :class="product.inStock ? 'text-success' : 'text-ink-subtle'"
    >
      {{ product.inStock ? t('store.product.inStock') : t('store.product.outOfStock') }}
    </p>
  </article>
</template>
