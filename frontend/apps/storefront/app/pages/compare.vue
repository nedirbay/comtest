<script setup lang="ts">
import { AppButton, EmptyState, PageHeader, formatMoney } from '@comtest/ui'

const { t, locale } = useI18n()
const localePath = useLocalePath()
const compare = useCompareList()
const products = useProductsBySlug(compare.items)

useHead({ title: computed(() => t('store.compare.title')) })

/** Attributes of all compared products, so that a missing one shows as empty in its column. */
const attributeRows = computed(() => {
  const rows = new Map<string, string>()

  for (const product of products.value) {
    for (const attribute of product.attributes) {
      rows.set(attribute.code, attribute.name)
    }
  }

  return [...rows]
})

const cheapest = (product: (typeof products.value)[number]) =>
  product.variants.reduce<(typeof product.variants)[number] | undefined>(
    (best, variant) => (!best || variant.price < best.price ? variant : best),
    undefined,
  )

const attributeValue = (product: (typeof products.value)[number], code: string) =>
  product.attributes.find((attribute) => attribute.code === code)?.value ?? '—'
</script>

<template>
  <PageHeader :title="t('store.compare.title')">
    <template v-if="products.length" #actions>
      <AppButton data-testid="compare-clear" @click="compare.clear()">
        {{ t('store.compare.clear') }}
      </AppButton>
    </template>
  </PageHeader>
  <ClientOnly>
    <EmptyState
      v-if="!compare.items.value.length"
      :title="t('store.compare.emptyTitle')"
      :description="t('store.compare.emptyText')"
    />
    <div v-else class="overflow-x-auto rounded-card border border-line bg-surface">
      <table class="w-full min-w-[32rem] border-collapse text-sm" data-testid="compare-table">
        <thead>
          <tr>
            <td class="w-40 p-3" />
            <th
              v-for="product in products"
              :key="product.id"
              scope="col"
              class="min-w-40 p-3 text-left align-top"
            >
              <NuxtLink
                :to="localePath(`/products/${product.slug}`)"
                class="block font-semibold text-ink hover:text-primary"
              >
                <ProductImage
                  :src="product.images[0]?.thumbnail"
                  alt=""
                  class="mb-2 w-28 border border-line"
                />
                {{ product.name }}
              </NuxtLink>
              <el-button link type="danger" class="mt-1" @click="compare.toggle(product.slug)">
                {{ t('store.compare.remove') }}
              </el-button>
            </th>
          </tr>
        </thead>
        <tbody class="divide-y divide-line border-t border-line">
          <tr>
            <th scope="row" class="p-3 text-left font-medium text-ink-muted">
              {{ t('store.compare.price') }}
            </th>
            <td v-for="product in products" :key="product.id" class="p-3 font-semibold text-ink">
              <template v-if="cheapest(product)">
                {{
                  formatMoney(
                    { amount: cheapest(product)!.price, currency: cheapest(product)!.currency },
                    locale,
                  )
                }}
              </template>
            </td>
          </tr>
          <tr>
            <th scope="row" class="p-3 text-left font-medium text-ink-muted">
              {{ t('store.compare.brand') }}
            </th>
            <td v-for="product in products" :key="product.id" class="p-3 text-ink">
              {{ product.brandName ?? '—' }}
            </td>
          </tr>
          <tr>
            <th scope="row" class="p-3 text-left font-medium text-ink-muted">
              {{ t('store.compare.stock') }}
            </th>
            <td v-for="product in products" :key="product.id" class="p-3 text-ink">
              {{
                product.variants.some((variant) => variant.inStock)
                  ? t('store.product.inStock')
                  : t('store.product.outOfStock')
              }}
            </td>
          </tr>
          <tr v-for="[code, name] in attributeRows" :key="code">
            <th scope="row" class="p-3 text-left font-medium text-ink-muted">{{ name }}</th>
            <td v-for="product in products" :key="product.id" class="p-3 text-ink">
              {{ attributeValue(product, code) }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </ClientOnly>
</template>
