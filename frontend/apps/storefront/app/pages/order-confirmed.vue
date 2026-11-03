<script setup lang="ts">
import { AppButton, ErrorState, SkeletonBlock } from '@comtest/ui'

const { t } = useI18n()
const localePath = useLocalePath()
const placed = usePlacedOrder()
const token = ref<string | null>(null)
const tracking = useOrderTracking(token)

useHead({ title: computed(() => t('store.confirmed.title')) })

onMounted(() => {
  // In the fragment, so it reaches neither servers nor the logs of anything linked from this page.
  token.value = new URLSearchParams(window.location.hash.slice(1)).get('token')
})

const order = computed(() => tracking.data.value)
const trackPath = computed(
  () => `${localePath('/track')}#token=${encodeURIComponent(token.value ?? '')}`,
)

/** Shown only for the order placed in this tab. A copied link has no business showing them. */
const instructions = computed(() =>
  placed.value && placed.value.number === order.value?.number ? placed.value.instructions : null,
)
</script>

<template>
  <div class="mx-auto max-w-3xl">
    <ErrorState
      v-if="tracking.isError.value"
      :error="tracking.error.value"
      :title="t('store.track.failedTitle')"
    />
    <SkeletonBlock v-else-if="!order" :lines="6" />
    <template v-else>
      <el-result
        icon="success"
        :title="t('store.confirmed.title')"
        :sub-title="t('store.confirmed.text')"
        data-testid="order-confirmed"
      />
      <el-alert
        v-if="instructions"
        :title="t('store.confirmed.instructions')"
        :description="instructions"
        type="info"
        :closable="false"
        show-icon
        class="mb-5"
        data-testid="payment-instructions"
      />
      <section class="rounded-card border border-line bg-surface p-4 sm:p-6">
        <TrackingDetails :order="order" />
      </section>
      <div class="mt-5 flex flex-wrap gap-2">
        <NuxtLink :to="trackPath">
          <AppButton variant="primary" data-testid="track-link">
            {{ t('store.track.title') }}
          </AppButton>
        </NuxtLink>
        <NuxtLink :to="localePath('/catalog')">
          <AppButton>{{ t('store.home.browse') }}</AppButton>
        </NuxtLink>
      </div>
      <p class="mt-3 text-sm text-ink-muted">{{ t('store.confirmed.keepLink') }}</p>
    </template>
  </div>
</template>
