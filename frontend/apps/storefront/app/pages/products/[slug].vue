<script setup lang="ts">
import { ApiError } from '@comtest/api-client'
import { AppButton, ErrorState, SafeHtml, useApiErrorMessage } from '@comtest/ui'

const { t } = useI18n()
const localePath = useLocalePath()
const route = useRoute()
const describeError = useApiErrorMessage()

const slug = computed(() => String(route.params.slug))
const query = useProduct(slug)
const categories = useCategories()

const first = await query.suspense()

if (first.error instanceof ApiError && first.error.status === 404) {
  throw createError({ status: 404, fatal: true })
}

const product = computed(() => query.data.value)

const variantId = ref<string | null>(null)

// Start on something that can be bought.
watch(
  product,
  (current) => {
    variantId.value =
      (current?.variants.find((item) => item.inStock) ?? current?.variants[0])?.id ?? null
  },
  { immediate: true },
)

const variant = computed(() => product.value?.variants.find((item) => item.id === variantId.value))
const quantity = ref(1)
const { add, drawerOpen } = useCart()

const crumbs = computed(() => {
  const trail =
    product.value?.categoryId && categories.data.value
      ? (findCategoryById(categories.data.value, product.value.categoryId)?.trail ?? [])
      : []

  return [
    ...trail.map((category) => ({
      label: category.name,
      to: localePath(`/catalog/${category.slug}`),
    })),
    { label: product.value?.name ?? '' },
  ]
})

async function addToCart(): Promise<void> {
  if (!variant.value || add.isPending.value) {
    return
  }

  await add.mutateAsync({ variantId: variant.value.id, quantity: quantity.value })
  drawerOpen.value = true
}

const { remember } = useRecentlyViewed()
const compare = useCompareList()

onMounted(() => remember(slug.value))
watch(slug, remember)

const origin = useRequestURL({ xForwardedHost: true }).origin
const firstImage = computed(() => product.value?.images[0])

usePageSeo(() => ({
  title: product.value?.metaTitle || product.value?.name,
  description:
    product.value?.metaDescription ||
    product.value?.shortDescription ||
    t('store.product.metaDescription', { name: product.value?.name ?? '' }),
  image: firstImage.value?.large ?? firstImage.value?.medium,
  type: 'product',
}))
useJsonLd('breadcrumbs', () => breadcrumbLd(crumbs.value, origin))
useJsonLd('product', () => {
  const current = product.value

  if (!current) {
    return null
  }

  return {
    '@type': 'Product',
    name: current.name,
    ...(current.shortDescription ? { description: current.shortDescription } : {}),
    ...(current.brandName ? { brand: { '@type': 'Brand', name: current.brandName } } : {}),
    image: current.images.flatMap((image) => image.large ?? image.medium ?? []),
    offers: current.variants.map((item) => ({
      '@type': 'Offer',
      sku: item.sku,
      price: item.price,
      priceCurrency: item.currency,
      availability: `https://schema.org/${item.inStock ? 'InStock' : 'OutOfStock'}`,
      url: `${origin}${route.path}`,
    })),
  }
})
</script>

<template>
  <ErrorState v-if="!product" :error="query.error.value" retryable @retry="query.refetch()" />
  <article v-else>
    <SiteBreadcrumbs :items="crumbs" />

    <div class="grid gap-6 lg:grid-cols-2 lg:gap-10">
      <ImageGallery :images="product.images" :name="product.name" />

      <div>
        <h1
          class="text-2xl font-bold tracking-tight text-ink sm:text-3xl"
          data-testid="product-name"
        >
          {{ product.name }}
        </h1>
        <NuxtLink
          v-if="product.brandName && product.brandId"
          :to="
            localePath({
              path: '/catalog',
              query: { brandId: product.brandId, brandName: product.brandName },
            })
          "
          class="mt-1 inline-block text-sm font-medium text-ink-muted hover:text-primary"
        >
          {{ product.brandName }}
        </NuxtLink>
        <p v-if="product.shortDescription" class="mt-3 text-ink-muted">
          {{ product.shortDescription }}
        </p>

        <PriceTag
          v-if="variant"
          class="mt-5"
          size="large"
          :amount="variant.price"
          :currency="variant.currency"
          :compare-at="variant.compareAtAmount"
        />
        <p
          v-if="variant"
          class="mt-1 text-sm font-medium"
          :class="variant.inStock ? 'text-success' : 'text-danger'"
          data-testid="stock-state"
        >
          {{ variant.inStock ? t('store.product.inStock') : t('store.product.outOfStock') }}
        </p>

        <VariantPicker v-model="variantId" :variants="product.variants" class="mt-5" />

        <div class="mt-6 flex flex-wrap items-center gap-3">
          <el-input-number
            v-model="quantity"
            :min="1"
            :max="99"
            :aria-label="t('store.cart.quantity')"
          />
          <AppButton
            variant="primary"
            size="large"
            class="flex-1 sm:flex-none"
            :disabled="!variant?.inStock"
            :loading="add.isPending.value"
            data-testid="add-to-cart"
            @click="addToCart"
          >
            {{ t('store.product.addToCart') }}
          </AppButton>
        </div>
        <p v-if="add.isError.value" class="mt-2 text-sm text-danger" role="alert">
          {{ describeError(add.error.value) }}
        </p>

        <div class="mt-4 flex flex-wrap items-center justify-between gap-3">
          <p v-if="variant?.sku" class="text-xs text-ink-subtle">SKU: {{ variant.sku }}</p>
          <ClientOnly>
            <el-button
              link
              type="primary"
              data-testid="compare-toggle"
              @click="compare.toggle(product!.slug)"
            >
              {{ compare.has(product.slug) ? t('store.compare.remove') : t('store.compare.add') }}
            </el-button>
          </ClientOnly>
        </div>
      </div>
    </div>

    <section
      v-if="product.description"
      class="mt-10 max-w-3xl"
      aria-labelledby="product-description"
    >
      <h2 id="product-description" class="mb-3 text-xl font-bold text-ink">
        {{ t('store.product.description') }}
      </h2>
      <SafeHtml :html="product.description" data-testid="product-description" />
    </section>

    <section
      v-if="product.attributes.length"
      class="mt-10 max-w-3xl"
      aria-labelledby="product-attributes"
    >
      <h2 id="product-attributes" class="mb-3 text-xl font-bold text-ink">
        {{ t('store.product.attributes') }}
      </h2>
      <dl class="divide-y divide-line rounded-card border border-line bg-surface">
        <div
          v-for="attribute in product.attributes"
          :key="attribute.code"
          class="grid grid-cols-2 gap-4 px-4 py-3 text-sm"
        >
          <dt class="text-ink-muted">{{ attribute.name }}</dt>
          <dd class="font-medium text-ink">{{ attribute.value }}</dd>
        </div>
      </dl>
    </section>

    <SimilarProducts
      v-if="product.categoryId"
      :category-id="product.categoryId"
      :exclude-id="product.id"
      class="mt-12"
    />
    <RecentlyViewed :exclude-slug="product.slug" class="mt-12" />
  </article>
</template>
