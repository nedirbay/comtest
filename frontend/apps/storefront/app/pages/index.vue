<script setup lang="ts">
import { EmptyState } from '@comtest/ui'

const NEW_PRODUCTS = 8

const { t } = useI18n()
const localePath = useLocalePath()
const store = useStoreInfo()
const categories = useCategories()
const newProducts = useProducts({ pageSize: NEW_PRODUCTS, sort: '-publishedAt' })

const origin = useRequestURL({ xForwardedHost: true }).origin

usePageSeo(() => ({ description: t('store.home.intro'), image: store.data.value?.logoUrl }))
useJsonLd('organization', () =>
  store.data.value
    ? {
        '@type': 'Organization',
        name: store.data.value.name,
        url: origin,
        ...(store.data.value.logoUrl ? { logo: store.data.value.logoUrl } : {}),
        ...(store.data.value.contactPhone ? { telephone: store.data.value.contactPhone } : {}),
        ...(store.data.value.contactEmail ? { email: store.data.value.contactEmail } : {}),
        ...(store.data.value.address ? { address: store.data.value.address } : {}),
      }
    : null,
)
</script>

<template>
  <EmptyState
    v-if="store.unavailable.value"
    :title="t('store.home.unavailableTitle')"
    :description="t('store.home.unavailableText')"
  />
  <div v-else-if="store.data.value" class="space-y-10">
    <section
      class="relative overflow-hidden rounded-panel bg-brand-900 px-6 py-12 text-white sm:px-12 sm:py-16"
      data-testid="store-hero"
    >
      <svg class="absolute inset-0 size-full opacity-[0.07]" aria-hidden="true">
        <defs>
          <pattern id="hero-gul" width="96" height="96" patternUnits="userSpaceOnUse">
            <path
              d="M31 8h34l23 23v34L65 88H31L8 65V31z"
              fill="none"
              stroke="#fff"
              stroke-width="2"
            />
            <path d="M48 24 72 48 48 72 24 48z" fill="none" stroke="#fff" stroke-width="2" />
            <path d="M48 38 58 48 48 58 38 48z" fill="#fff" />
          </pattern>
        </defs>
        <rect width="100%" height="100%" fill="url(#hero-gul)" />
      </svg>
      <div class="relative max-w-2xl">
        <h1 class="text-3xl font-bold tracking-tight sm:text-display">
          {{ t('store.home.welcome', { name: store.data.value.name }) }}
        </h1>
        <p class="mt-3 text-brand-100">{{ t('store.home.intro') }}</p>
        <NuxtLink
          :to="localePath('/catalog')"
          class="mt-6 inline-flex h-11 items-center rounded-control bg-white px-5 font-semibold text-brand-900 hover:bg-brand-50"
        >
          {{ t('store.home.browse') }}
        </NuxtLink>
      </div>
    </section>

    <section v-if="categories.data.value?.length" aria-labelledby="home-categories">
      <h2 id="home-categories" class="mb-4 text-xl font-bold text-ink">
        {{ t('store.home.categories') }}
      </h2>
      <ul class="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
        <li v-for="category in categories.data.value" :key="category.id">
          <NuxtLink
            :to="localePath(`/catalog/${category.slug}`)"
            class="flex h-full min-h-20 items-center rounded-card border border-line bg-surface p-4 font-semibold text-ink transition-colors hover:border-primary hover:text-primary"
          >
            {{ category.name }}
          </NuxtLink>
        </li>
      </ul>
    </section>

    <section v-if="newProducts.data.value?.items.length" aria-labelledby="home-new">
      <div class="mb-4 flex items-baseline justify-between gap-4">
        <h2 id="home-new" class="text-xl font-bold text-ink">{{ t('store.home.newProducts') }}</h2>
        <NuxtLink
          :to="localePath('/catalog')"
          class="text-sm font-semibold text-primary hover:underline"
        >
          {{ t('store.home.seeAll') }}
        </NuxtLink>
      </div>
      <ProductGrid :products="newProducts.data.value.items" />
    </section>
  </div>
</template>
