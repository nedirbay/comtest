<script setup lang="ts">
import { EmptyState, ErrorState, PageHeader } from '@comtest/ui'
import { Filter } from '@element-plus/icons-vue'

const props = defineProps<{ title: string; description?: string | null; categoryId?: string }>()

const PAGE_SIZE = 24

const { t } = useI18n()
const { filters, apply, activeCount } = useListingFilters()
const panelOpen = ref(false)

const products = useProducts(() => ({
  page: filters.value.page,
  pageSize: PAGE_SIZE,
  sort: filters.value.sort,
  categoryId: props.categoryId,
  brandId: filters.value.brandId,
  minPrice: filters.value.minPrice,
  maxPrice: filters.value.maxPrice,
  q: filters.value.q,
}))

const result = computed(() => products.data.value)

function goToPage(page: number): void {
  apply({ page })
  window.scrollTo({ top: 0, behavior: 'smooth' })
}
</script>

<template>
  <div>
    <slot name="breadcrumbs" />
    <PageHeader :title="title" :description="description ?? undefined">
      <template #actions>
        <el-button
          class="lg:!hidden"
          :icon="Filter"
          data-testid="open-filters"
          @click="panelOpen = true"
        >
          {{ t('store.filters.title') }}{{ activeCount ? ` (${activeCount})` : '' }}
        </el-button>
        <!-- Native on purpose: phones show their own picker, and it needs no hydration to work. -->
        <select
          :value="filters.sort"
          :aria-label="t('store.sort.label')"
          class="h-10 w-48 rounded-control border border-line-strong bg-surface px-3 text-sm text-ink"
          data-testid="sort"
          @change="apply({ sort: ($event.target as HTMLSelectElement).value as SortOption })"
        >
          <option v-for="option in SORT_OPTIONS" :key="option" :value="option">
            {{ t(`store.sort.${option}`) }}
          </option>
        </select>
      </template>
    </PageHeader>

    <div class="lg:grid lg:grid-cols-[15rem_minmax(0,1fr)] lg:gap-8">
      <aside class="hidden lg:block">
        <div class="sticky top-24 rounded-card border border-line bg-surface p-4">
          <ProductFilters />
        </div>
      </aside>

      <section aria-labelledby="listing-heading" aria-live="polite">
        <!-- Cards use h3, and headings should not skip a level for those who navigate by them. -->
        <h2 id="listing-heading" class="sr-only">{{ t('store.listing.heading') }}</h2>
        <ErrorState
          v-if="products.isError.value && !result"
          :error="products.error.value"
          retryable
          @retry="products.refetch()"
        />
        <ProductGridSkeleton v-else-if="!result" />
        <EmptyState
          v-else-if="result.items.length === 0"
          :title="t('store.listing.emptyTitle')"
          :description="t('store.listing.emptyText')"
        />
        <template v-else>
          <p class="mb-3 text-sm text-ink-muted" data-testid="result-count">
            {{ t('store.listing.count', { count: result.totalCount }) }}
          </p>
          <ProductGrid
            :products="result.items"
            :class="{ 'opacity-60': products.isPlaceholderData.value }"
          />
          <div v-if="result.totalCount > result.pageSize" class="mt-6 flex justify-center">
            <el-pagination
              :total="result.totalCount"
              :current-page="result.page"
              :page-size="result.pageSize"
              layout="prev, pager, next"
              background
              :pager-count="5"
              @update:current-page="goToPage"
            />
          </div>
        </template>
      </section>
    </div>

    <el-drawer
      v-model="panelOpen"
      direction="btt"
      size="auto"
      :title="t('store.filters.title')"
      data-testid="filter-panel"
    >
      <ProductFilters @applied="panelOpen = false" />
    </el-drawer>
  </div>
</template>
