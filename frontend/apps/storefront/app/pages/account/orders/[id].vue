<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import {
  AppButton,
  AppDialog,
  ErrorState,
  SkeletonBlock,
  formatPhone,
  normalizeStatus,
  useApiErrorMessage,
} from '@comtest/ui'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

definePageMeta({ middleware: 'auth', requiresAuth: true })

const { t, locale } = useI18n()
const localePath = useLocalePath()
const api = useApi()
const route = useRoute()
const queryClient = useQueryClient()
const describeError = useApiErrorMessage()
const { regionLabel } = useRegions()

const id = computed(() => String(route.params.id))

const order = useQuery({
  queryKey: ['account', 'orders', 'detail', id, locale],
  queryFn: () =>
    unwrap(api.client.GET('/api/v1/account/orders/{id}', { params: { path: { id: id.value } } })),
  refetchInterval: orderRefreshInterval,
})

useHead({ title: computed(() => order.data.value?.number ?? t('store.account.orders')) })

/** The server has the final say. This only avoids offering what it would certainly refuse. */
const canCancel = computed(() => normalizeStatus(order.data.value?.status) === 'pending')
const confirmOpen = ref(false)

const cancel = useMutation({
  mutationFn: () =>
    unwrap(
      api.client.POST('/api/v1/account/orders/{id}/cancel', { params: { path: { id: id.value } } }),
    ),
  onSuccess: async () => {
    confirmOpen.value = false
    await queryClient.invalidateQueries({ queryKey: ['account', 'orders'] })
  },
})
</script>

<template>
  <AccountShell :title="t('store.orders.detailTitle')">
    <NuxtLink
      :to="localePath('/account/orders')"
      class="mb-4 inline-block text-sm font-semibold text-primary hover:underline"
    >
      ← {{ t('store.account.orders') }}
    </NuxtLink>
    <ErrorState
      v-if="order.isError.value"
      :error="order.error.value"
      retryable
      @retry="order.refetch()"
    />
    <SkeletonBlock v-else-if="!order.data.value" :lines="6" class="max-w-3xl" />
    <div v-else class="max-w-3xl space-y-4">
      <section class="rounded-card border border-line bg-surface p-4 sm:p-6">
        <TrackingDetails :order="order.data.value" />
      </section>
      <section class="rounded-card border border-line bg-surface p-4 text-sm sm:p-6">
        <h2 class="mb-2 font-bold text-ink">{{ t('store.orders.shipTo') }}</h2>
        <p class="text-ink-muted">
          {{ order.data.value.shipTo.recipient }}, {{ formatPhone(order.data.value.shipTo.phone) }}
          <template v-if="order.data.value.shipTo.line1">
            <br />
            {{ regionLabel(order.data.value.shipTo.region) }}, {{ order.data.value.shipTo.line1 }}
          </template>
        </p>
      </section>
      <AppButton
        v-if="canCancel"
        variant="danger"
        plain
        data-testid="cancel-order"
        @click="confirmOpen = true"
      >
        {{ t('store.orders.cancel') }}
      </AppButton>
    </div>

    <AppDialog
      v-model="confirmOpen"
      :title="t('store.orders.cancel')"
      confirm-variant="danger"
      :confirm-text="t('store.orders.cancelConfirm')"
      :loading="cancel.isPending.value"
      @confirm="cancel.mutate()"
    >
      <p class="text-sm text-ink-muted">{{ t('store.orders.cancelText') }}</p>
      <p v-if="cancel.isError.value" class="mt-2 text-sm text-danger" role="alert">
        {{ describeError(cancel.error.value) }}
      </p>
    </AppDialog>
  </AccountShell>
</template>
