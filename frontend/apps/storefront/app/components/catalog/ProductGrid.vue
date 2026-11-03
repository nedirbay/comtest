<script setup lang="ts">
import type { Schema } from '@comtest/api-client'

defineProps<{ products: readonly Schema<'StoreProductListItemDto'>[] }>()
</script>

<template>
  <ul class="grid grid-cols-2 gap-3 sm:gap-4 md:grid-cols-3 xl:grid-cols-4">
    <li v-for="product in products" :key="product.id" class="flex">
      <ProductCard :product="product" class="w-full" />
    </li>
  </ul>
</template>
