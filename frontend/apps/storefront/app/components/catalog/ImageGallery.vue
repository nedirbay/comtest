<script setup lang="ts">
import type { Schema } from '@comtest/api-client'
import { safeUrl } from '@comtest/ui'

const props = defineProps<{ images: readonly Schema<'ProductImageDto'>[]; name: string }>()

const active = ref(0)

const current = computed(() => props.images[active.value])

/** The widths the API renders every picture in, so a phone need not download the largest. */
const sourceSet = computed(() => {
  const image = current.value
  const sizes: [string | null | undefined, number][] = [
    [image?.thumbnail, 320],
    [image?.medium, 800],
    [image?.large, 1600],
  ]

  return (
    sizes
      .flatMap(([url, width]) => {
        const safe = safeUrl(url)

        return safe ? [`${safe} ${width}w`] : []
      })
      .join(', ') || undefined
  )
})

watch(
  () => props.images,
  () => {
    active.value = 0
  },
)
</script>

<template>
  <div>
    <ProductImage
      :src="current?.large ?? current?.medium ?? current?.thumbnail"
      :srcset="sourceSet"
      sizes="(min-width: 1024px) 50vw, 100vw"
      :alt="current?.altText || name"
      eager
      class="border border-line bg-surface"
      data-testid="gallery-main"
    />
    <ul v-if="images.length > 1" class="mt-3 flex gap-2 overflow-x-auto pb-1">
      <li v-for="(image, index) in images" :key="image.mediaAssetId" class="w-16 shrink-0">
        <button
          type="button"
          class="block w-full rounded-control border-2 transition-colors"
          :class="
            index === active ? 'border-primary' : 'border-transparent hover:border-line-strong'
          "
          :aria-label="image.altText || name"
          :aria-current="index === active"
          @click="active = index"
        >
          <ProductImage :src="image.thumbnail" alt="" />
        </button>
      </li>
    </ul>
  </div>
</template>
