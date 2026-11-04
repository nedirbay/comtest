<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi, useAuthStore } from '@comtest/auth'
import {
  AppButton,
  AppTable,
  ErrorState,
  formatPhone,
  PageHeader,
  safeUrl,
  SkeletonBlock,
  StatusTag,
  useApiErrorMessage,
  type TableColumn,
} from '@comtest/ui'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink, useRoute } from 'vue-router'
import { Permission } from '@/app/permissions'
import { usePageTitle } from '@/features/shell/useShellStore'
import { useCustomerPrivacy } from '@/shared/composables/useCustomerPrivacy'
import { useDrawerItem } from '@/shared/composables/useDrawerItem'
import { useFormat } from '@/shared/composables/useFormat'
import OrderStatusForm from '../components/OrderStatusForm.vue'
import PaymentPanel from '../components/PaymentPanel.vue'
import { isUndoingStatus, nextStatuses, ORDERS_KEY } from '../queries'

const { t, te } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const route = useRoute()
const format = useFormat()
const privacy = useCustomerPrivacy()
const queryClient = useQueryClient()
const describeError = useApiErrorMessage()

const orderId = String(route.params.id)

const detail = useQuery({
  queryKey: [...ORDERS_KEY, 'detail', orderId],
  queryFn: () =>
    unwrap(api.client.GET('/api/v1/admin/orders/{id}', { params: { path: { id: orderId } } })),
})

const order = computed(() => detail.data.value?.order)
const status = computed(() => order.value?.status ?? '')

usePageTitle(() => order.value?.number)

const canWrite = computed(() => auth.can(Permission.OrdersWrite))
const canManage = computed(() => auth.can(Permission.OrdersManage))

// The server refuses cancelling and returning without `orders.manage`, so those are not offered.
const actions = computed(() =>
  nextStatuses(status.value).filter((next) =>
    isUndoingStatus(next) ? canManage.value : canWrite.value,
  ),
)

const statusDrawer = useDrawerItem<Schema<'OrderStatus'>>()

const itemColumns = computed<TableColumn[]>(() => [
  { key: 'productName', label: t('admin.stock.product'), minWidth: 160 },
  { key: 'sku', label: t('admin.productForm.sku'), minWidth: 120 },
  { key: 'unitPrice', label: t('admin.order.unitPrice'), minWidth: 110, align: 'right' },
  { key: 'quantity', label: t('admin.order.quantity'), minWidth: 70, align: 'right' },
  { key: 'lineTotal', label: t('admin.orders.total'), minWidth: 120, align: 'right' },
])

const items = computed(() =>
  (order.value?.items ?? []).map((item) => ({ ...item, id: item.variantId })),
)

const addressLine = computed(() => {
  const to = order.value?.shipTo

  if (!to) {
    return ''
  }

  const region = to.region && te(`regions.${to.region}`) ? t(`regions.${to.region}`) : to.region

  return [region, to.district, to.city, to.line1, to.line2, to.postalCode]
    .filter(Boolean)
    .join(', ')
})

const trackingUrl = computed(() => safeUrl(order.value?.tracking.url))

const noteText = ref('')
const noteError = ref<string | null>(null)

const addNote = useMutation({
  mutationFn: (text: string) =>
    unwrap(
      api.client.POST('/api/v1/admin/orders/{id}/notes', {
        params: { path: { id: orderId } },
        body: { text },
      }),
    ),
  onSuccess: (saved) => {
    queryClient.setQueryData([...ORDERS_KEY, 'detail', orderId], saved)
    noteText.value = ''
    noteError.value = null
  },
  onError: (error) => {
    noteError.value = describeError(error)
  },
})

function submitNote(): void {
  const text = noteText.value.trim()

  if (text) {
    addNote.mutate(text)
  }
}

// The history names people by id. Only those who manage staff may turn an id into a name.
const staff = useQuery({
  queryKey: ['admin', 'staff', 'members'],
  enabled: computed(() => auth.can(Permission.StaffManage)),
  queryFn: () => unwrap(api.client.GET('/api/v1/admin/staff')),
})

function personName(id: string | null): string {
  if (!id) {
    return t('admin.order.bySystem')
  }

  return (
    staff.data.value?.find((member) => member.userId === id)?.fullName ?? t('admin.order.byStaff')
  )
}

const actionLabel = (next: string) => t(`admin.order.action_${next}`)
</script>

<template>
  <ErrorState
    v-if="detail.isError.value"
    :error="detail.error.value"
    retryable
    @retry="detail.refetch()"
  />
  <SkeletonBlock v-else-if="detail.isPending.value" :lines="8" />
  <template v-else-if="order && detail.data.value">
    <PageHeader
      :title="t('admin.order.heading', { number: order.number })"
      :description="format.dateTime(order.placedAt)"
    >
      <template #actions>
        <span data-testid="order-status">
          <StatusTag kind="orderStatus" :value="order.status" />
        </span>
        <span data-testid="order-payment-state">
          <StatusTag kind="paymentState" :value="order.paymentState" />
        </span>
      </template>
    </PageHeader>

    <div
      v-if="actions.length"
      class="mb-4 flex flex-wrap gap-2 rounded-card border border-line bg-surface p-3"
      data-testid="order-actions"
    >
      <AppButton
        v-for="next in actions"
        :key="next"
        :variant="isUndoingStatus(next) ? 'danger' : 'primary'"
        :data-testid="`order-action-${next}`"
        @click="statusDrawer.show(next)"
      >
        {{ actionLabel(next) }}
      </AppButton>
    </div>

    <div class="grid gap-4 xl:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
      <div class="min-w-0 space-y-4">
        <AppTable :columns="itemColumns" :rows="items">
          <template #cell-productName="{ row }">
            <RouterLink
              v-if="auth.can(Permission.CatalogRead)"
              :to="{ name: 'product-edit', params: { id: row.productId } }"
              class="font-semibold text-primary"
            >
              {{ row.productName }}
            </RouterLink>
            <span v-else class="font-semibold text-ink">{{ row.productName }}</span>
            <span v-if="Object.keys(row.options).length" class="block text-xs text-ink-subtle">
              {{ Object.values(row.options).join(', ') }}
            </span>
          </template>
          <template #cell-unitPrice="{ row }">
            {{ format.money(row.unitPrice, order.totals.currency) }}
          </template>
          <template #cell-lineTotal="{ row }">
            {{ format.money(row.lineTotal, order.totals.currency) }}
          </template>
        </AppTable>

        <section class="rounded-card border border-line bg-surface p-4">
          <dl class="ml-auto max-w-sm space-y-1 text-sm" data-testid="order-totals">
            <div class="flex justify-between gap-4">
              <dt class="text-ink-muted">{{ t('admin.order.subtotal') }}</dt>
              <dd class="tabular-nums text-ink">
                {{ format.money(order.totals.subtotal, order.totals.currency) }}
              </dd>
            </div>
            <div v-if="order.totals.discount" class="flex justify-between gap-4">
              <dt class="text-ink-muted">{{ t('admin.order.discount') }}</dt>
              <dd class="tabular-nums text-ink">
                {{ format.money(order.totals.discount, order.totals.currency) }}
              </dd>
            </div>
            <div v-if="order.totals.tax" class="flex justify-between gap-4">
              <dt class="text-ink-muted">{{ t('admin.order.tax') }}</dt>
              <dd class="tabular-nums text-ink">
                {{ format.money(order.totals.tax, order.totals.currency) }}
              </dd>
            </div>
            <div class="flex justify-between gap-4">
              <dt class="text-ink-muted">{{ t('admin.order.shipping') }}</dt>
              <dd class="tabular-nums text-ink">
                {{ format.money(order.totals.shipping, order.totals.currency) }}
              </dd>
            </div>
            <div class="flex justify-between gap-4 border-t border-line pt-1.5 text-base font-bold">
              <dt class="text-ink">{{ t('admin.orders.total') }}</dt>
              <dd class="tabular-nums text-ink">
                {{ format.money(order.totals.total, order.totals.currency) }}
              </dd>
            </div>
          </dl>
        </section>

        <section class="rounded-card border border-line bg-surface p-4">
          <h2 class="mb-3 font-bold text-ink">{{ t('admin.order.history') }}</h2>
          <el-timeline class="!pl-0" data-testid="order-history">
            <el-timeline-item
              v-for="(change, index) in detail.data.value.history"
              :key="index"
              :timestamp="format.dateTime(change.at)"
              placement="top"
            >
              <StatusTag kind="orderStatus" :value="change.to" />
              <span class="ml-2 text-xs text-ink-subtle">{{ personName(change.by) }}</span>
              <p v-if="change.note" class="mt-1 break-words text-sm text-ink">{{ change.note }}</p>
            </el-timeline-item>
          </el-timeline>
        </section>

        <section class="rounded-card border border-line bg-surface p-4">
          <h2 class="mb-3 font-bold text-ink">{{ t('admin.order.notes') }}</h2>
          <p v-if="detail.data.value.notes.length === 0" class="mb-3 text-sm text-ink-muted">
            {{ t('admin.order.noNotes') }}
          </p>
          <ul v-else class="mb-3 space-y-2">
            <li
              v-for="note in detail.data.value.notes"
              :key="note.id"
              class="rounded-control border border-line p-3 text-sm"
              data-testid="order-note"
            >
              <p class="whitespace-pre-line break-words text-ink">{{ note.text }}</p>
              <p class="mt-1 text-xs text-ink-subtle">
                {{ format.dateTime(note.at) }} · {{ personName(note.by) }}
              </p>
            </li>
          </ul>
          <template v-if="canWrite">
            <el-input
              v-model="noteText"
              type="textarea"
              :rows="2"
              maxlength="2000"
              :placeholder="t('admin.order.notePlaceholder')"
              :aria-label="t('admin.order.notePlaceholder')"
              data-testid="note-input"
            />
            <p v-if="noteError" class="mt-1 text-sm text-danger" role="alert">{{ noteError }}</p>
            <AppButton
              class="mt-2"
              :loading="addNote.isPending.value"
              :disabled="!noteText.trim()"
              data-testid="note-add"
              @click="submitNote"
            >
              {{ t('admin.order.addNote') }}
            </AppButton>
          </template>
        </section>
      </div>

      <div class="min-w-0 space-y-4">
        <section class="rounded-card border border-line bg-surface p-4" data-testid="order-contact">
          <h2 class="mb-3 font-bold text-ink">{{ t('admin.order.customerTitle') }}</h2>
          <p class="font-semibold text-ink">
            <RouterLink
              v-if="detail.data.value.customerUserId && privacy.canSee.value"
              :to="{
                name: 'customer-profile',
                params: { userId: detail.data.value.customerUserId },
              }"
              class="text-primary"
            >
              {{ order.contact.name }}
            </RouterLink>
            <RouterLink
              v-else-if="privacy.canSee.value"
              :to="{ name: 'customer-guest', params: { orderId } }"
              class="text-primary"
            >
              {{ order.contact.name }}
            </RouterLink>
            <template v-else>{{ privacy.name(order.contact.name) }}</template>
          </p>
          <p class="mt-1 text-sm text-ink-muted" data-testid="contact-phone">
            {{
              privacy.canSee.value
                ? formatPhone(order.contact.phone)
                : privacy.phone(order.contact.phone)
            }}
          </p>
          <p v-if="order.contact.email" class="break-words text-sm text-ink-muted">
            {{ privacy.email(order.contact.email) }}
          </p>
          <p v-if="!privacy.canSee.value" class="mt-2 text-xs text-ink-subtle">
            {{ t('admin.order.maskedHint') }}
          </p>
          <p v-if="order.customerNote" class="mt-3 rounded-control bg-canvas p-2 text-sm text-ink">
            {{ order.customerNote }}
          </p>
        </section>

        <section class="rounded-card border border-line bg-surface p-4">
          <h2 class="mb-3 font-bold text-ink">{{ t('admin.order.shippingTitle') }}</h2>
          <p class="text-sm font-semibold text-ink">{{ order.shippingMethodName }}</p>
          <p class="text-xs text-ink-subtle">
            <StatusTag kind="shippingType" :value="order.shippingType" />
          </p>
          <p class="mt-2 text-sm text-ink">{{ privacy.name(order.shipTo.recipient) }}</p>
          <p class="text-sm text-ink-muted">{{ privacy.address(addressLine) }}</p>
          <template v-if="order.tracking.carrier || order.tracking.number || order.tracking.url">
            <h3 class="mb-1 mt-3 text-sm font-semibold text-ink">
              {{ t('admin.order.trackingTitle') }}
            </h3>
            <p class="text-sm text-ink" data-testid="order-tracking">
              {{ [order.tracking.carrier, order.tracking.number].filter(Boolean).join(' · ') }}
            </p>
            <a
              v-if="trackingUrl"
              :href="trackingUrl"
              target="_blank"
              rel="noopener noreferrer"
              class="break-all text-sm font-semibold text-primary"
              data-testid="order-tracking-link"
            >
              {{ t('admin.order.openTracking') }}
            </a>
          </template>
        </section>

        <PaymentPanel :order-id="orderId" />
      </div>
    </div>

    <OrderStatusForm
      v-if="statusDrawer.mounted.value && statusDrawer.item.value"
      :key="statusDrawer.session.value"
      v-model="statusDrawer.open.value"
      :order-id="orderId"
      :order-number="order.number"
      :status="statusDrawer.item.value"
      @closed="statusDrawer.closed()"
    />
  </template>
</template>
