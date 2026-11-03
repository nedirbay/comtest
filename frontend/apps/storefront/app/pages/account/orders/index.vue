<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import {
  AppButton,
  EmptyState,
  ErrorState,
  SkeletonBlock,
  StatusTag,
  formatDateTime,
  formatMoney,
} from '@comtest/ui'
import { keepPreviousData, useQuery } from '@tanstack/vue-query'

definePageMeta({ middleware: 'auth', requiresAuth: true })

const PAGE_SIZE = 10

const { t, locale } = useI18n()
const localePath = useLocalePath()
const api = useApi()
const route = useRoute()
const router = useRouter()

useHead({ title: computed(() => t('store.account.orders')) })

const page = computed(() => {
  const value = Number(route.query.page)

  return Number.isInteger(value) && value > 0 ? value : 1
})

const orders = useQuery({
  queryKey: ['account', 'orders', page, locale],
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/account/orders', {
        params: { query: { page: page.value, pageSize: PAGE_SIZE } },
      }),
    ),
  placeholderData: keepPreviousData,
})
</script>

<template>
  <AccountShell :title="t('store.account.orders')">
    <ErrorState
      v-if="orders.isError.value"
      :error="orders.error.value"
      retryable
      @retry="orders.refetch()"
    />
    <SkeletonBlock v-else-if="!orders.data.value" :lines="6" class="max-w-3xl" />
    <EmptyState
      v-else-if="!orders.data.value.items.length"
      :title="t('store.orders.emptyTitle')"
      :description="t('store.orders.emptyText')"
    >
      <NuxtLink :to="localePath('/catalog')">
        <AppButton variant="primary">{{ t('store.home.browse') }}</AppButton>
      </NuxtLink>
    </EmptyState>
    <template v-else>
      <ul class="max-w-3xl space-y-3">
        <li v-for="order in orders.data.value.items" :key="order.id">
          <NuxtLink
            :to="localePath(`/account/orders/${order.id}`)"
            class="block rounded-card border border-line bg-surface p-4 transition-colors hover:border-primary"
            data-testid="order-row"
          >
            <div class="flex flex-wrap items-center justify-between gap-2">
              <span class="font-bold text-ink">{{ order.number }}</span>
              <StatusTag kind="orderStatus" :value="order.status" />
            </div>
            <div
              class="mt-1 flex flex-wrap items-center justify-between gap-2 text-sm text-ink-muted"
            >
              <span>
                {{ formatDateTime(order.placedAt, locale) }} ·
                {{ t('store.orders.items', { count: order.itemCount }) }}
              </span>
              <span class="font-semibold text-ink">
                {{ formatMoney({ amount: order.total, currency: order.currency }, locale) }}
              </span>
            </div>
          </NuxtLink>
        </li>
      </ul>
      <div
        v-if="orders.data.value.totalCount > orders.data.value.pageSize"
        class="mt-5 flex max-w-3xl justify-center"
      >
        <el-pagination
          :total="orders.data.value.totalCount"
          :current-page="orders.data.value.page"
          :page-size="orders.data.value.pageSize"
          layout="prev, pager, next"
          background
          @update:current-page="router.push({ query: { page: $event } })"
        />
      </div>
    </template>
  </AccountShell>
</template>
