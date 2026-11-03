<script setup lang="ts">
import { safeUrl } from '@comtest/ui'

const props = defineProps<{ excludeSlug?: string }>()

const { t } = useI18n()
const localePath = useLocalePath()
const { items } = useRecentlyViewed()

const slugs = computed(() => items.value.filter((slug) => slug !== props.excludeSlug))
const products = useProductsBySlug(slugs)
</script>

<template>
  <section v-if="products.length" aria-labelledby="recently-viewed" data-testid="recently-viewed">
    <h2 id="recently-viewed" class="mb-4 text-xl font-bold text-ink">
      {{ t('store.recent.title') }}
    </h2>
    <ul class="flex gap-3 overflow-x-auto pb-2">
      <li v-for="product in products" :key="product.id" class="w-36 shrink-0">
        <NuxtLink
          :to="localePath(`/products/${product.slug}`)"
          class="block text-sm font-medium text-ink hover:text-primary"
        >
          <ProductImage
            :src="safeUrl(product.images[0]?.thumbnail)"
            alt=""
            class="mb-2 border border-line"
          />
          <span class="line-clamp-2">{{ product.name }}</span>
        </NuxtLink>
      </li>
    </ul>
  </section>
</template>
