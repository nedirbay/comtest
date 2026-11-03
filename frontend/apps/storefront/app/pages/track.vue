<script setup lang="ts">
import { EmptyState, ErrorState, PageHeader, SkeletonBlock } from '@comtest/ui'

const { t } = useI18n()
const token = ref<string | null>(null)
const checked = ref(false)
const tracking = useOrderTracking(token)

useHead({ title: computed(() => t('store.track.title')) })

// Kept in the address on purpose: a guest has no other way back to their order.
onMounted(() => {
  token.value = new URLSearchParams(window.location.hash.slice(1)).get('token')
  checked.value = true
})
</script>

<template>
  <div class="mx-auto max-w-3xl">
    <PageHeader :title="t('store.track.title')" />
    <SkeletonBlock v-if="!checked" :lines="5" />
    <EmptyState
      v-else-if="!token"
      :title="t('store.track.noTokenTitle')"
      :description="t('store.track.noTokenText')"
    />
    <ErrorState
      v-else-if="tracking.isError.value"
      :error="tracking.error.value"
      :title="t('store.track.failedTitle')"
    />
    <SkeletonBlock v-else-if="!tracking.data.value" :lines="5" />
    <section v-else class="rounded-card border border-line bg-surface p-4 sm:p-6">
      <TrackingDetails :order="tracking.data.value" />
    </section>
  </div>
</template>
