<script setup lang="ts">
import { AppButton } from '@comtest/ui'

const emit = defineEmits<{ applied: [] }>()

const { t } = useI18n()
const { filters, apply, clear, activeCount } = useListingFilters()

const min = ref<number | undefined>(filters.value.minPrice)
const max = ref<number | undefined>(filters.value.maxPrice)

// Back and forward change the address, and the fields have to follow it.
watch(filters, (current) => {
  min.value = current.minPrice
  max.value = current.maxPrice
})

function submit(): void {
  apply({ minPrice: min.value || undefined, maxPrice: max.value || undefined })
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

    <div v-if="filters.brandId && filters.brandName">
      <p class="mb-2 text-sm font-bold text-ink">{{ t('store.filters.brand') }}</p>
      <el-tag
        closable
        disable-transitions
        @close="apply({ brandId: undefined, brandName: undefined })"
      >
        {{ filters.brandName }}
      </el-tag>
    </div>

    <div class="flex gap-2">
      <AppButton type="submit" variant="primary" class="flex-1">
        {{ t('store.filters.apply') }}
      </AppButton>
      <AppButton v-if="activeCount > 0" @click="reset">{{ t('store.filters.clear') }}</AppButton>
    </div>
  </form>
</template>
