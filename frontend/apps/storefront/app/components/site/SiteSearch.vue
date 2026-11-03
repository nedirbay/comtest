<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { Search } from '@element-plus/icons-vue'

const MAX_LENGTH = 100
const MIN_SUGGEST_LENGTH = 2
const SUGGESTIONS = 5
const DEBOUNCE_MS = 250

const { t } = useI18n()
const localePath = useLocalePath()
const route = useRoute()
const api = useApi()
const listId = useId()

const text = ref(typeof route.query.q === 'string' ? route.query.q : '')
const items = ref<Schema<'StoreProductListItemDto'>[]>([])
const open = ref(false)
const active = ref(-1)

let timer: ReturnType<typeof setTimeout> | undefined
let latestRequest = 0

watch(
  () => route.fullPath,
  () => {
    text.value = typeof route.query.q === 'string' ? route.query.q : ''
    close()
  },
)

function close(): void {
  clearTimeout(timer)
  latestRequest += 1
  open.value = false
  active.value = -1
}

/** A failed suggestion must never get in the way of typing, so it simply shows nothing. */
async function load(): Promise<void> {
  const q = text.value.trim()
  const request = (latestRequest += 1)

  if (q.length < MIN_SUGGEST_LENGTH) {
    open.value = false

    return
  }

  try {
    const result = await unwrap(
      api.client.GET('/api/v1/catalog/products', {
        params: { query: { q, pageSize: SUGGESTIONS } },
      }),
    )

    // A slower answer to older text must not replace what the newer one showed.
    if (request === latestRequest) {
      items.value = result.items
      open.value = result.items.length > 0
      active.value = -1
    }
  } catch {
    open.value = false
  }
}

function onInput(): void {
  clearTimeout(timer)
  timer = setTimeout(load, DEBOUNCE_MS)
}

function openProduct(slug: string): void {
  close()
  void navigateTo(localePath(`/products/${slug}`))
}

function submit(): void {
  const choice = items.value[active.value]

  if (open.value && choice) {
    openProduct(choice.slug)

    return
  }

  const q = text.value.trim()

  close()

  if (q) {
    void navigateTo(localePath({ path: '/search', query: { q } }))
  }
}

function move(step: number): void {
  if (open.value && items.value.length > 0) {
    active.value = (active.value + step + items.value.length) % items.value.length
  }
}
</script>

<template>
  <form role="search" class="relative w-full" @submit.prevent="submit">
    <!-- A native input, as a combobox's ARIA attributes have to sit on the input element itself. -->
    <el-icon
      class="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-ink-subtle"
      aria-hidden="true"
    >
      <Search />
    </el-icon>
    <input
      v-model="text"
      type="text"
      name="q"
      role="combobox"
      autocomplete="off"
      aria-autocomplete="list"
      :aria-expanded="open"
      :aria-controls="listId"
      :aria-activedescendant="active >= 0 ? `${listId}-${active}` : undefined"
      :maxlength="MAX_LENGTH"
      :placeholder="t('store.search.placeholder')"
      :aria-label="t('store.search.placeholder')"
      class="h-10 w-full rounded-control border border-line-strong bg-surface pr-3 pl-9 text-sm text-ink transition-colors placeholder:text-ink-subtle hover:border-ink-subtle focus:border-primary"
      data-testid="search-input"
      @input="onInput"
      @blur="close"
      @keydown.down.prevent="move(1)"
      @keydown.up.prevent="move(-1)"
      @keydown.esc="close"
    />
    <ul
      v-show="open"
      :id="listId"
      role="listbox"
      :aria-label="t('store.search.placeholder')"
      class="absolute inset-x-0 top-full z-30 mt-1 overflow-hidden rounded-control border border-line bg-surface-raised py-1 shadow-raised"
    >
      <li
        v-for="(item, index) in items"
        :id="`${listId}-${index}`"
        :key="item.id"
        role="option"
        :aria-selected="index === active"
        class="cursor-pointer truncate px-3 py-2 text-sm text-ink"
        :class="{ 'bg-primary-soft': index === active }"
        data-testid="search-suggestion"
        @mousedown.prevent="openProduct(item.slug)"
        @mouseenter="active = index"
      >
        {{ item.name }}
      </li>
    </ul>
  </form>
</template>
