<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { useApi, useAuthStore } from '@comtest/auth'
import { Search } from '@element-plus/icons-vue'
import { keepPreviousData, useQuery } from '@tanstack/vue-query'
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter, type RouteLocationRaw } from 'vue-router'
import { visibleSections } from '@/app/navigation'
import { Permission } from '@/app/permissions'
import { useCustomerPrivacy } from '@/shared/composables/useCustomerPrivacy'

const SEARCH_DELAY_MS = 250
const MIN_TERM_LENGTH = 2
const MAX_PER_GROUP = 5
const API_PAGE_SIZE = 100

const open = defineModel<boolean>({ required: true })

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const router = useRouter()
const privacy = useCustomerPrivacy()

const input = ref<{ focus: () => void } | null>(null)
const term = ref('')
const debounced = ref('')
const highlighted = ref(0)
let timer: ReturnType<typeof setTimeout> | undefined

watch(term, (value) => {
  clearTimeout(timer)
  timer = setTimeout(() => {
    debounced.value = value.trim()
  }, SEARCH_DELAY_MS)
  highlighted.value = 0
})

watch(open, (isOpen) => {
  if (isOpen) {
    term.value = ''
    debounced.value = ''
    highlighted.value = 0
  }
})

// Only once the dialog has opened, because until then its focus trap takes the focus for itself.
function focusInput(): void {
  input.value?.focus()
}

const searching = computed(() => open.value && debounced.value.length >= MIN_TERM_LENGTH)

// The admin list has no text search, and the public one sees only published products. Together
// they cover every published product, and every recent draft by reading the newest hundred.
const publishedProducts = useQuery({
  queryKey: ['palette', 'published', debounced],
  enabled: computed(() => searching.value && auth.can(Permission.CatalogRead)),
  placeholderData: keepPreviousData,
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/catalog/products', {
        params: { query: { q: debounced.value, pageSize: MAX_PER_GROUP } },
      }),
    ),
})

const recentProducts = useQuery({
  queryKey: ['palette', 'recent-products'],
  enabled: computed(() => open.value && auth.can(Permission.CatalogRead)),
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/catalog/products', {
        params: { query: { pageSize: API_PAGE_SIZE, sort: '-createdAt' } },
      }),
    ),
})

const orders = useQuery({
  queryKey: ['palette', 'orders', debounced],
  enabled: computed(() => searching.value && auth.can(Permission.OrdersRead)),
  placeholderData: keepPreviousData,
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/orders', {
        params: { query: { q: debounced.value, pageSize: MAX_PER_GROUP } },
      }),
    ),
})

const customers = useQuery({
  queryKey: ['palette', 'customers', debounced],
  enabled: computed(() => searching.value && auth.can(Permission.CustomersRead)),
  placeholderData: keepPreviousData,
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/customers', {
        params: { query: { q: debounced.value, pageSize: MAX_PER_GROUP } },
      }),
    ),
})

interface Result {
  id: string
  group: string
  label: string
  hint?: string
  to: RouteLocationRaw | (() => Promise<RouteLocationRaw | null>)
}

const lowered = computed(() => term.value.trim().toLocaleLowerCase())

const results = computed<Result[]>(() => {
  const found: Result[] = []
  const needle = lowered.value

  const pages = visibleSections((permission) => auth.can(permission))
    .flatMap((section) => section.items)
    .filter((item) => t(item.labelKey).toLocaleLowerCase().includes(needle))

  for (const item of pages) {
    found.push({
      id: `page-${item.name}`,
      group: t('admin.palette.pages'),
      label: t(item.labelKey),
      to: { name: item.name },
    })
  }

  if (!searching.value) {
    return found
  }

  const products = new Map<string, { name: string; slug: string }>()

  for (const product of recentProducts.data.value?.items ?? []) {
    const name = product.name ?? product.slug

    if (`${name} ${product.slug}`.toLocaleLowerCase().includes(needle)) {
      products.set(product.id, { name, slug: product.slug })
    }
  }

  for (const product of publishedProducts.data.value?.items ?? []) {
    products.set(product.id, { name: product.name, slug: product.slug })
  }

  for (const [id, product] of [...products].slice(0, MAX_PER_GROUP)) {
    found.push({
      id: `product-${id}`,
      group: t('admin.palette.products'),
      label: product.name,
      hint: product.slug,
      to: { name: 'product-edit', params: { id } },
    })
  }

  for (const order of orders.data.value?.items ?? []) {
    found.push({
      id: `order-${order.id}`,
      group: t('admin.palette.orders'),
      label: order.number,
      hint: privacy.name(order.contactName),
      to: { name: 'order-detail', params: { id: order.id } },
    })
  }

  for (const customer of customers.data.value?.items ?? []) {
    const { userId, phone } = customer

    found.push({
      id: `customer-${userId ?? phone}`,
      group: t('admin.palette.customers'),
      label: customer.name,
      hint: phone,
      to: userId
        ? { name: 'customer-profile', params: { userId } }
        : async () => {
            const page = await unwrap(
              api.client.GET('/api/v1/admin/orders', {
                params: { query: { q: phone, pageSize: 1 } },
              }),
            )
            const orderId = page.items[0]?.id

            return orderId ? { name: 'customer-guest', params: { orderId } } : null
          },
    })
  }

  return found
})

const groups = computed(() => {
  const byName = new Map<string, Array<Result & { index: number }>>()

  results.value.forEach((result, index) => {
    byName.set(result.group, [...(byName.get(result.group) ?? []), { ...result, index }])
  })

  return [...byName].map(([name, items]) => ({ name, items }))
})

const loading = computed(
  () =>
    searching.value &&
    (publishedProducts.isFetching.value || orders.isFetching.value || customers.isFetching.value),
)

async function choose(result: Result | undefined): Promise<void> {
  if (!result) {
    return
  }

  open.value = false

  const target = typeof result.to === 'function' ? await result.to() : result.to

  if (target) {
    await router.push(target)
  }
}

function onKeydown(event: Event | KeyboardEvent): void {
  if (!(event instanceof KeyboardEvent)) {
    return
  }

  const count = results.value.length

  if (event.key === 'ArrowDown') {
    event.preventDefault()
    highlighted.value = count ? (highlighted.value + 1) % count : 0
  } else if (event.key === 'ArrowUp') {
    event.preventDefault()
    highlighted.value = count ? (highlighted.value - 1 + count) % count : 0
  } else if (event.key === 'Enter') {
    event.preventDefault()
    void choose(results.value[highlighted.value])
  }
}
</script>

<template>
  <el-dialog
    v-model="open"
    :show-close="false"
    width="min(40rem, calc(100vw - 2rem))"
    top="8vh"
    :aria-label="t('admin.palette.title')"
    class="palette-dialog"
    append-to-body
    @opened="focusInput"
  >
    <template #header>
      <el-input
        ref="input"
        v-model="term"
        size="large"
        :prefix-icon="Search"
        :placeholder="t('admin.palette.placeholder')"
        :aria-label="t('admin.palette.title')"
        role="combobox"
        aria-expanded="true"
        aria-controls="palette-results"
        :aria-activedescendant="
          results[highlighted] ? `palette-${results[highlighted]?.id}` : undefined
        "
        data-testid="palette-input"
        @keydown="onKeydown"
      />
    </template>
    <div id="palette-results" class="max-h-[60dvh] overflow-y-auto" role="listbox">
      <template v-for="group in groups" :key="group.name">
        <p class="px-2 pb-1 pt-3 text-xs font-semibold uppercase tracking-wide text-ink-subtle">
          {{ group.name }}
        </p>
        <button
          v-for="result in group.items"
          :id="`palette-${result.id}`"
          :key="result.id"
          type="button"
          role="option"
          :aria-selected="highlighted === result.index"
          class="flex w-full items-center justify-between gap-3 rounded-control px-3 py-2 text-left text-sm"
          :class="highlighted === result.index ? 'bg-primary-soft text-primary' : 'text-ink'"
          data-testid="palette-result"
          @mousemove="highlighted = result.index"
          @click="choose(result)"
        >
          <span class="min-w-0 truncate font-semibold">{{ result.label }}</span>
          <span v-if="result.hint" class="min-w-0 shrink truncate text-xs text-ink-subtle">
            {{ result.hint }}
          </span>
        </button>
      </template>
      <p v-if="results.length === 0" class="px-2 py-6 text-center text-sm text-ink-muted">
        {{ loading ? t('common.loading') : t('admin.palette.empty') }}
      </p>
    </div>
    <p class="mt-3 border-t border-line pt-2 text-xs text-ink-subtle">
      {{ t('admin.palette.hint') }}
    </p>
  </el-dialog>
</template>

<style>
.palette-dialog .el-dialog__header {
  padding: 0;
}
</style>
