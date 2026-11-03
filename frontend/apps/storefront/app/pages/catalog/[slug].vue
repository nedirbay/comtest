<script setup lang="ts">
const { t } = useI18n()
const localePath = useLocalePath()
const route = useRoute()
const categories = useCategories()

// A category that does not exist has to answer 404, or search engines would index an empty page.
const tree = await categories.suspense().then((result) => result.data ?? [])
const initial = findCategoryBySlug(tree, String(route.params.slug))

if (tree.length > 0 && !initial) {
  throw createError({ status: 404, fatal: true })
}

const match = computed(() =>
  findCategoryBySlug(categories.data.value ?? [], String(route.params.slug)),
)

const crumbs = computed(() => [
  { label: t('store.nav.allProducts'), to: localePath('/catalog') },
  ...(match.value?.trail ?? []).map((category, index, trail) => ({
    label: category.name,
    to: index < trail.length - 1 ? localePath(`/catalog/${category.slug}`) : undefined,
  })),
])

const origin = useRequestURL({ xForwardedHost: true }).origin

usePageSeo(() => ({
  title: match.value?.category.name,
  description:
    match.value?.category.description ||
    t('store.listing.metaDescription', { name: match.value?.category.name ?? '' }),
}))
useJsonLd('breadcrumbs', () => (match.value ? breadcrumbLd(crumbs.value, origin) : null))
</script>

<template>
  <ProductListing
    v-if="match"
    :key="match.category.id"
    :title="match.category.name"
    :description="match.category.description"
    :category-id="match.category.id"
  >
    <template #breadcrumbs>
      <SiteBreadcrumbs :items="crumbs" />
    </template>
  </ProductListing>
</template>
