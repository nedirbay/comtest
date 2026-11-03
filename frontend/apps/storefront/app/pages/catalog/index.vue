<script setup lang="ts">
const { t } = useI18n()

usePageSeo(() => ({
  title: t('store.nav.allProducts'),
  description: t('store.listing.metaDescription', { name: t('store.nav.allProducts') }),
}))
</script>

<template>
  <ProductListing :title="t('store.nav.allProducts')">
    <template #breadcrumbs>
      <SiteBreadcrumbs :items="[{ label: t('store.nav.allProducts') }]" />
    </template>
  </ProductListing>
</template>
