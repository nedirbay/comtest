<script setup lang="ts">
const props = defineProps<{ categoryId: string; excludeId: string }>()

const COUNT = 5

const { t } = useI18n()
const query = useProducts(() => ({
  categoryId: props.categoryId,
  pageSize: COUNT,
  sort: '-publishedAt',
}))

const products = computed(() =>
  (query.data.value?.items ?? []).filter((item) => item.id !== props.excludeId).slice(0, COUNT - 1),
)
</script>

<template>
  <section v-if="products.length" aria-labelledby="similar-products" data-testid="similar-products">
    <h2 id="similar-products" class="mb-4 text-xl font-bold text-ink">
      {{ t('store.similar.title') }}
    </h2>
    <ProductGrid :products="products" />
  </section>
</template>
