<script setup lang="ts">
const { t } = useI18n()
const { filters } = useListingFilters()

const title = computed(() =>
  filters.value.q
    ? t('store.search.resultsFor', { query: filters.value.q })
    : t('store.search.title'),
)

// Results depend on what was typed, which makes them poor pages for a search engine.
useSeoMeta({ robots: 'noindex' })
useHead({ title })
</script>

<template>
  <div>
    <SiteSearch class="mb-6 lg:hidden" />
    <ProductListing v-if="filters.q" :title="title">
      <template #breadcrumbs>
        <SiteBreadcrumbs :items="[{ label: t('store.search.title') }]" />
      </template>
    </ProductListing>
    <p v-else class="py-10 text-center text-ink-muted">{{ t('store.search.hint') }}</p>
  </div>
</template>
