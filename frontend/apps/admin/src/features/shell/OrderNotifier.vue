<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi, useAuthStore } from '@comtest/auth'
import { Bell } from '@element-plus/icons-vue'
import { useQuery, useQueryClient } from '@tanstack/vue-query'
import { ElNotification } from 'element-plus'
import 'element-plus/es/components/notification/style/css'
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { Permission } from '@/app/permissions'
import { useFormat } from '@/shared/composables/useFormat'

/**
 * There is no push channel from the API, so this asks once a minute. TanStack Query stops asking
 * while the tab is hidden and asks again when it comes back.
 */
const POLL_MS = 60_000
const PAGE_SIZE = 5

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const router = useRouter()
const queryClient = useQueryClient()
const format = useFormat()

type OrderRow = Schema<'OrderListItemDto'>

const enabled = computed(() => auth.hasActiveTenant && auth.can(Permission.OrdersRead))

const latest = useQuery({
  queryKey: ['order-watch'],
  enabled,
  staleTime: 0,
  refetchInterval: POLL_MS,
  refetchIntervalInBackground: false,
  retry: false,
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/orders', {
        params: { query: { page: 1, pageSize: PAGE_SIZE, sort: '-placedAt' } },
      }),
    ),
})

/** Orders that arrived since this page was opened, newest first. Lost on reload, by design. */
const unseen = ref<OrderRow[]>([])
let baseline: { placedAt: number; totalCount: number } | null = null
const known = new Set<string>()

function openOrder(id: string): void {
  unseen.value = unseen.value.filter((order) => order.id !== id)
  void router.push({ name: 'order-detail', params: { id } })
}

watch(latest.data, (page) => {
  if (!page) {
    return
  }

  const newest = page.items[0] ? new Date(page.items[0].placedAt).getTime() : 0

  // The first answer only marks where "now" is. What was there already is not news.
  if (!baseline) {
    baseline = { placedAt: newest, totalCount: page.totalCount }

    return
  }

  const since = baseline.placedAt
  const arrived = page.items.filter(
    (order) => new Date(order.placedAt).getTime() > since && !known.has(order.id),
  )

  if (arrived.length === 0) {
    return
  }

  for (const order of arrived) {
    known.add(order.id)
  }

  unseen.value = [...arrived, ...unseen.value]

  const first = arrived[0] as OrderRow

  ElNotification({
    title: t('admin.notify.newOrderTitle'),
    message: t('admin.notify.newOrderText', { number: first.number }),
    type: 'info',
    duration: 10_000,
    position: 'bottom-right',
    customClass: 'order-notification',
    onClick: () => openOrder(first.id),
  })

  // Any list of orders on screen is out of date now.
  void queryClient.invalidateQueries({ queryKey: ['admin', 'orders'] })
})

function markAllRead(): void {
  unseen.value = []
}
</script>

<template>
  <el-popover v-if="enabled" trigger="click" placement="bottom-end" :width="300">
    <template #reference>
      <button
        type="button"
        class="inline-flex size-10 items-center justify-center rounded-control text-ink-muted hover:bg-primary-soft"
        :aria-label="t('admin.notify.bell')"
        data-testid="order-bell"
      >
        <el-badge
          :value="unseen.length"
          :hidden="unseen.length === 0"
          :max="99"
          data-testid="order-badge"
        >
          <el-icon :size="20"><Bell /></el-icon>
        </el-badge>
      </button>
    </template>
    <p class="mb-1 text-sm font-semibold text-ink">{{ t('admin.notify.title') }}</p>
    <p class="mb-2 text-xs text-ink-subtle">{{ t('admin.notify.pollingHint') }}</p>
    <p v-if="unseen.length === 0" class="py-2 text-sm text-ink-muted">
      {{ t('admin.notify.none') }}
    </p>
    <ul v-else class="max-h-72 space-y-1 overflow-y-auto">
      <li v-for="order in unseen" :key="order.id">
        <button
          type="button"
          class="w-full rounded-control px-2 py-1.5 text-left hover:bg-primary-soft"
          @click="openOrder(order.id)"
        >
          <span class="block text-sm font-semibold text-ink">{{ order.number }}</span>
          <span class="block text-xs text-ink-muted">{{ format.dateTime(order.placedAt) }}</span>
        </button>
      </li>
    </ul>
    <el-button v-if="unseen.length" class="mt-2 w-full" text @click="markAllRead">
      {{ t('admin.notify.markRead') }}
    </el-button>
  </el-popover>
</template>
