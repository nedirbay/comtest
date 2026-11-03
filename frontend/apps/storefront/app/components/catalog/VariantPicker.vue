<script setup lang="ts">
import type { Schema } from '@comtest/api-client'

type Variant = Schema<'StoreVariantDto'>

const props = defineProps<{ variants: readonly Variant[] }>()
const selectedId = defineModel<string | null>({ required: true })

const { t, te } = useI18n()

const options = (variant: Variant) => (variant.options ?? {}) as Record<string, string>

const selected = computed(() => props.variants.find((variant) => variant.id === selectedId.value))

/** Each option name with the values that exist for it, such as colour: black, white. */
const axes = computed(() => {
  const found = new Map<string, Set<string>>()

  for (const variant of props.variants) {
    for (const [name, value] of Object.entries(options(variant))) {
      found.set(name, (found.get(name) ?? new Set()).add(value))
    }
  }

  return [...found].map(([name, values]) => ({ name, values: [...values] }))
})

/** The variant this choice leads to, keeping the other options as chosen when that is possible. */
function variantFor(name: string, value: string): Variant | undefined {
  const wanted = { ...(selected.value ? options(selected.value) : {}), [name]: value }
  const candidates = props.variants.filter((variant) => options(variant)[name] === value)

  return (
    candidates.find((variant) =>
      Object.entries(wanted).every(([key, item]) => options(variant)[key] === item),
    ) ??
    candidates.find((variant) => variant.inStock) ??
    candidates[0]
  )
}

const axisLabel = (name: string) =>
  te(`store.product.options.${name}`) ? t(`store.product.options.${name}`) : name
</script>

<template>
  <div v-if="variants.length > 1" class="space-y-4">
    <fieldset v-for="axis in axes" :key="axis.name">
      <legend class="mb-2 text-sm font-semibold text-ink">
        {{ axisLabel(axis.name) }}
        <span v-if="selected">: {{ options(selected)[axis.name] }}</span>
      </legend>
      <div class="flex flex-wrap gap-2">
        <button
          v-for="value in axis.values"
          :key="value"
          type="button"
          class="min-h-10 min-w-10 rounded-control border px-3 text-sm font-medium transition-colors"
          :class="[
            selected && options(selected)[axis.name] === value
              ? 'border-primary bg-primary-soft text-primary'
              : 'border-line-strong text-ink hover:border-primary',
            !variantFor(axis.name, value)?.inStock && 'text-ink-subtle line-through',
          ]"
          :aria-pressed="selected ? options(selected)[axis.name] === value : false"
          @click="selectedId = variantFor(axis.name, value)?.id ?? selectedId"
        >
          {{ value }}
        </button>
      </div>
    </fieldset>
  </div>
</template>
