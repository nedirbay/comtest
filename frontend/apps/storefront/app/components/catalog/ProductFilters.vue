<script setup lang="ts">
import { AppButton } from '@comtest/ui'

const props = defineProps<{ categoryId?: string }>()
const emit = defineEmits<{ applied: [] }>()

const { t } = useI18n()
const { filters, apply, clear, activeCount } = useListingFilters()
const brands = useBrands()
const brandField = useId()
const attributes = useAttributeFilters(() => props.categoryId)

const min = ref<number | undefined>(filters.value.minPrice)
const max = ref<number | undefined>(filters.value.maxPrice)
const brand = ref(filters.value.brandId ?? '')
const attr = ref<string[]>(filters.value.attr ?? [])

// Back and forward change the address, and the fields have to follow it.
watch(filters, (current) => {
  min.value = current.minPrice
  max.value = current.maxPrice
  brand.value = current.brandId ?? ''
  attr.value = current.attr ?? []
})

function valueLabel(dataType: string, value: string): string {
  if (dataType === 'boolean') {
    return t(value === 'true' ? 'store.filters.yes' : 'store.filters.no')
  }

  return value
}

function submit(): void {
  apply({
    minPrice: min.value || undefined,
    maxPrice: max.value || undefined,
    brandId: brand.value || undefined,
    attr: attr.value,
  })
  emit('applied')
}

function reset(): void {
  clear()
  emit('applied')
}
</script>

<template>
  <form class="space-y-5" @submit.prevent="submit">
    <fieldset>
      <legend class="mb-2 text-sm font-bold text-ink">{{ t('store.filters.price') }}</legend>
      <div class="flex items-center gap-2">
        <el-input-number
          v-model="min"
          :min="0"
          :controls="false"
          :placeholder="t('store.filters.from')"
          :aria-label="t('store.filters.from')"
          class="!w-full"
          data-testid="min-price"
        />
        <span aria-hidden="true" class="text-ink-subtle">–</span>
        <el-input-number
          v-model="max"
          :min="0"
          :controls="false"
          :placeholder="t('store.filters.to')"
          :aria-label="t('store.filters.to')"
          class="!w-full"
          data-testid="max-price"
        />
      </div>
    </fieldset>

    <div v-if="brands.data.value?.length">
      <label :for="brandField" class="mb-2 block text-sm font-bold text-ink">
        {{ t('store.filters.brand') }}
      </label>
      <!-- Native on purpose, like the sort control: it needs no hydration to work. -->
      <select
        :id="brandField"
        v-model="brand"
        class="h-10 w-full rounded-control border border-line-strong bg-surface px-3 text-sm text-ink"
        data-testid="brand-filter"
      >
        <option value="">{{ t('store.filters.allBrands') }}</option>
        <option v-for="item in brands.data.value" :key="item.id" :value="item.id">
          {{ item.name }}
        </option>
      </select>
    </div>

    <fieldset
      v-for="attribute in attributes.data.value"
      :key="attribute.code"
      :data-testid="`attribute-filter-${attribute.code}`"
    >
      <legend class="mb-2 text-sm font-bold text-ink">{{ attribute.name }}</legend>
      <!-- Native on purpose: a filter must work before and without hydration. -->
      <label
        v-for="item in attribute.values"
        :key="item.value"
        class="flex cursor-pointer items-center gap-2 py-1 text-sm text-ink"
      >
        <input
          v-model="attr"
          type="checkbox"
          class="size-4 accent-primary"
          :value="`${attribute.code}:${item.value}`"
        />
        <span class="min-w-0 flex-1 truncate">
          {{ valueLabel(attribute.dataType, item.value) }}
        </span>
        <span class="text-xs tabular-nums text-ink-subtle">{{ item.count }}</span>
      </label>
    </fieldset>

    <div class="flex gap-2">
      <AppButton type="submit" variant="primary" class="flex-1">
        {{ t('store.filters.apply') }}
      </AppButton>
      <AppButton v-if="activeCount > 0" @click="reset">{{ t('store.filters.clear') }}</AppButton>
    </div>
  </form>
</template>
