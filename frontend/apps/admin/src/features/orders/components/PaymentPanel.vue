<script setup lang="ts">
import { ApiError, unwrap, type Schema } from '@comtest/api-client'
import { useApi, useAuthStore } from '@comtest/auth'
import { AppButton, ErrorState, normalizeStatus, SkeletonBlock, StatusTag } from '@comtest/ui'
import { useQuery, useQueryClient } from '@tanstack/vue-query'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { Permission } from '@/app/permissions'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useDrawerItem } from '@/shared/composables/useDrawerItem'
import { useFormat } from '@/shared/composables/useFormat'
import { toastSuccess } from '@/shared/toast'
import { ORDERS_KEY } from '../queries'
import ConfirmPaymentForm from './ConfirmPaymentForm.vue'
import RefundForm from './RefundForm.vue'
import RejectRefundForm from './RejectRefundForm.vue'

const props = defineProps<{ orderId: string }>()

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const format = useFormat()
const confirm = useConfirm()
const queryClient = useQueryClient()

const payment = useQuery({
  queryKey: [...ORDERS_KEY, 'detail', computed(() => props.orderId), 'payment'],
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/orders/{id}/payment', {
        params: { path: { id: props.orderId } },
      }),
    ),
})

const status = computed(() => normalizeStatus(payment.data.value?.status))
const canConfirm = computed(
  () => status.value === 'pending' && auth.can(Permission.PaymentsConfirm),
)
const canRefund = computed(
  () =>
    (status.value === 'paid' || status.value === 'partiallyRefunded') &&
    auth.can(Permission.PaymentsRefund),
)
const canDecide = computed(() => auth.can(Permission.PaymentsRefundApprove))

const notFound = computed(
  () => payment.error.value instanceof ApiError && payment.error.value.status === 404,
)

const confirming = useDrawerItem<true>()
const refunding = useDrawerItem<true>()
const rejecting = useDrawerItem<Schema<'RefundDto'>>()

function approve(refund: Schema<'RefundDto'>, currency: string): void {
  confirm.ask({
    title: t('admin.order.approveRefund'),
    message: t('admin.order.approveConfirm', { amount: format.money(refund.amount, currency) }),
    confirmText: t('admin.order.approveRefund'),
    danger: true,
    action: async () => {
      await unwrap(
        api.client.POST('/api/v1/admin/orders/{id}/payment/refunds/{refundId}/approve', {
          params: { path: { id: props.orderId, refundId: refund.id } },
        }),
      )
      await queryClient.invalidateQueries({ queryKey: ORDERS_KEY })
      toastSuccess(t('admin.order.refundApproved'))
    },
  })
}

const isPendingApproval = (refund: Schema<'RefundDto'>) =>
  normalizeStatus(refund.status) === 'pendingApproval'

// A second person has to approve: the server refuses the one who asked.
const isOwnRequest = (refund: Schema<'RefundDto'>) => refund.requestedBy === auth.claims?.userId
</script>

<template>
  <section class="rounded-card border border-line bg-surface p-4" data-testid="payment-panel">
    <h2 class="mb-3 font-bold text-ink">{{ t('admin.order.paymentTitle') }}</h2>
    <p v-if="notFound" class="text-sm text-ink-muted">{{ t('admin.order.noPayment') }}</p>
    <ErrorState
      v-else-if="payment.isError.value"
      :error="payment.error.value"
      retryable
      @retry="payment.refetch()"
    />
    <SkeletonBlock v-else-if="payment.isPending.value" :lines="4" />
    <template v-else-if="payment.data.value">
      <dl class="space-y-1.5 text-sm">
        <div class="flex items-center justify-between gap-3">
          <dt class="text-ink-muted">{{ t('admin.orders.status') }}</dt>
          <dd data-testid="payment-status">
            <StatusTag kind="paymentStatus" :value="payment.data.value.status" />
          </dd>
        </div>
        <div class="flex items-center justify-between gap-3">
          <dt class="text-ink-muted">{{ t('admin.orders.paymentMethod') }}</dt>
          <dd><StatusTag kind="paymentMethod" :value="payment.data.value.method" /></dd>
        </div>
        <div class="flex justify-between gap-3">
          <dt class="text-ink-muted">{{ t('admin.order.paidAmount') }}</dt>
          <dd class="font-semibold tabular-nums text-ink">
            {{ format.money(payment.data.value.amount, payment.data.value.currency) }}
          </dd>
        </div>
        <div v-if="payment.data.value.refundedAmount" class="flex justify-between gap-3">
          <dt class="text-ink-muted">{{ t('admin.order.refundedAmount') }}</dt>
          <dd class="tabular-nums text-ink" data-testid="payment-refunded">
            {{ format.money(payment.data.value.refundedAmount, payment.data.value.currency) }}
          </dd>
        </div>
        <div v-if="payment.data.value.pendingRefundAmount" class="flex justify-between gap-3">
          <dt class="text-ink-muted">{{ t('admin.order.pendingRefund') }}</dt>
          <dd class="tabular-nums text-ink">
            {{ format.money(payment.data.value.pendingRefundAmount, payment.data.value.currency) }}
          </dd>
        </div>
        <div v-if="payment.data.value.reference" class="flex justify-between gap-3">
          <dt class="text-ink-muted">{{ t('admin.order.paymentReference') }}</dt>
          <dd class="min-w-0 truncate text-ink">{{ payment.data.value.reference }}</dd>
        </div>
        <div v-if="payment.data.value.paidAt" class="flex justify-between gap-3">
          <dt class="text-ink-muted">{{ t('admin.order.paidAt') }}</dt>
          <dd class="text-right text-ink">{{ format.dateTime(payment.data.value.paidAt) }}</dd>
        </div>
      </dl>

      <div v-if="canConfirm || canRefund" class="mt-4 flex flex-wrap gap-2">
        <AppButton
          v-if="canConfirm"
          variant="primary"
          data-testid="payment-confirm"
          @click="confirming.show()"
        >
          {{ t('admin.order.confirmPayment') }}
        </AppButton>
        <AppButton v-if="canRefund" data-testid="refund-request" @click="refunding.show()">
          {{ t('admin.order.requestRefund') }}
        </AppButton>
      </div>

      <template v-if="payment.data.value.refunds.length">
        <h3 class="mb-2 mt-5 text-sm font-semibold text-ink">{{ t('admin.order.refunds') }}</h3>
        <ul class="space-y-2">
          <li
            v-for="refund in payment.data.value.refunds"
            :key="refund.id"
            class="rounded-control border border-line p-3 text-sm"
            data-testid="refund"
          >
            <div class="flex flex-wrap items-center justify-between gap-2">
              <span class="font-semibold tabular-nums text-ink">
                {{ format.money(refund.amount, refund.currency) }}
              </span>
              <StatusTag kind="refundStatus" :value="refund.status" />
            </div>
            <p class="mt-1 break-words text-ink-muted">{{ refund.reason }}</p>
            <div v-if="isPendingApproval(refund) && canDecide" class="mt-2 flex flex-wrap gap-2">
              <AppButton
                v-if="!isOwnRequest(refund)"
                variant="primary"
                data-testid="refund-approve"
                @click="approve(refund, payment.data.value?.currency ?? refund.currency)"
              >
                {{ t('admin.order.approveRefund') }}
              </AppButton>
              <AppButton data-testid="refund-reject" @click="rejecting.show(refund)">
                {{ t('admin.order.rejectRefund') }}
              </AppButton>
            </div>
            <p
              v-if="isPendingApproval(refund) && isOwnRequest(refund)"
              class="mt-2 text-xs text-ink-subtle"
              data-testid="refund-own"
            >
              {{ t('admin.order.ownRefund') }}
            </p>
          </li>
        </ul>
      </template>

      <template v-if="payment.data.value.events.length">
        <h3 class="mb-2 mt-5 text-sm font-semibold text-ink">
          {{ t('admin.order.paymentEvents') }}
        </h3>
        <ul class="space-y-1 text-xs text-ink-muted">
          <li v-for="(event, index) in payment.data.value.events" :key="index">
            {{ format.dateTime(event.at) }} ·
            {{ t(`admin.order.event_${normalizeStatus(event.type)}`) }}
            <template v-if="event.note">· {{ event.note }}</template>
          </li>
        </ul>
      </template>

      <ConfirmPaymentForm
        v-if="confirming.mounted.value"
        :key="confirming.session.value"
        v-model="confirming.open.value"
        :order-id="orderId"
        @closed="confirming.closed()"
      />
      <RefundForm
        v-if="refunding.mounted.value"
        :key="refunding.session.value"
        v-model="refunding.open.value"
        :order-id="orderId"
        :payment="payment.data.value"
        @closed="refunding.closed()"
      />
      <RejectRefundForm
        v-if="rejecting.mounted.value && rejecting.item.value"
        :key="rejecting.session.value"
        v-model="rejecting.open.value"
        :order-id="orderId"
        :refund-id="rejecting.item.value.id"
        @closed="rejecting.closed()"
      />
    </template>
  </section>
</template>
