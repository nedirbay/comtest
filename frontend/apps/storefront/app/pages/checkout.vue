<script setup lang="ts">
import { ApiError, unwrap, type Schema } from '@comtest/api-client'
import { useApi, useAuthStore } from '@comtest/auth'
import {
  AppButton,
  AppField,
  AppForm,
  EmptyState,
  ErrorState,
  PageHeader,
  SkeletonBlock,
  formatMoney,
  safeUrl,
  useApiErrorMessage,
  useForm,
} from '@comtest/ui'
import { useQuery, useQueryClient } from '@tanstack/vue-query'
import { z } from '@comtest/ui/zod'

useHead({ title: computed(() => useNuxtApp().$i18n.t('store.checkout.title')) })

const { t, locale } = useI18n()
const localePath = useLocalePath()
const api = useApi()
const auth = useAuthStore()
const describeError = useApiErrorMessage()
const queryClient = useQueryClient()
const { cart, invalidate, isLoading: cartLoading } = useCart()
const { regionLabel } = useRegions()

await auth.bootstrap()

const steps = computed(() => [
  t('store.checkout.steps.contact'),
  t('store.checkout.steps.address'),
  t('store.checkout.steps.shipping'),
  t('store.checkout.steps.payment'),
  t('store.checkout.steps.review'),
])
const step = ref(0)

// --- Contact
const contactSchema = z.object({
  name: z.string().trim().min(1, 'validation.required').max(200, 'validation.tooLong'),
  phone: phoneField,
  email: z.union([z.literal(''), z.email('validation.email')]),
})
const contactForm = useForm({
  schema: contactSchema,
  initial: { name: '', phone: '', email: '' },
  translate: (key) => t(key),
  describeError,
  submit: async () => {
    step.value = 1
  },
})

watchEffect(() => {
  const profile = auth.profile

  if (profile) {
    contactForm.values.name ||= profile.fullName
    contactForm.values.email ||= profile.email
    contactForm.values.phone ||= profile.phoneNumber ?? ''
  }
})

// --- Address
type Delivery = 'courier' | 'pickup'

const delivery = ref<Delivery>('courier')
const addressId = ref<string>()
const savedAddresses = useQuery({
  queryKey: ['account', 'addresses'],
  queryFn: () => unwrap(api.client.GET('/api/v1/account/addresses')),
  enabled: computed(() => auth.isAuthenticated),
})

watchEffect(() => {
  const list = savedAddresses.data.value

  if (list?.length && addressId.value === undefined) {
    addressId.value = (list.find((item) => item.isDefault) ?? list[0])?.id
  }
})

const addressForm = useForm({
  schema: addressSchema,
  initial: { ...EMPTY_ADDRESS },
  translate: (key) => t(key),
  describeError,
  submit: async () => {
    step.value = 2
  },
})

const useNewAddress = computed(
  () => addressId.value === 'new' || !savedAddresses.data.value?.length,
)
const chosenSaved = computed(() =>
  savedAddresses.data.value?.find((item) => item.id === addressId.value),
)

function continueFromAddress(): void {
  if (delivery.value === 'pickup' || !useNewAddress.value) {
    step.value = 2

    return
  }

  void addressForm.handleSubmit()
}

watch(
  () => contactForm.values,
  (contact) => {
    addressForm.values.recipient ||= contact.name
    addressForm.values.phone ||= contact.phone
  },
  { deep: true },
)

// --- Quote: the server's answer is the only source of options and totals.
const region = computed(() => {
  if (delivery.value === 'pickup') {
    return null
  }

  return useNewAddress.value
    ? addressForm.values.region || null
    : (chosenSaved.value?.address.region ?? null)
})
const shippingMethodId = ref<string>()
const paymentCode = ref<string>()

const quote = useQuery({
  queryKey: ['checkout', 'quote', region, shippingMethodId, locale],
  queryFn: () =>
    unwrap(
      api.client.POST('/api/v1/checkout/quote', {
        body: { region: region.value, shippingMethodId: shippingMethodId.value ?? null },
      }),
    ),
  placeholderData: (previous) => previous,
  staleTime: 0,
})

const shippingOptions = computed(() =>
  (quote.data.value?.shippingOptions ?? []).filter(
    (option) => delivery.value === 'courier' || option.type.toLowerCase() === 'pickup',
  ),
)

// An option can disappear when the region changes, and a chosen method must exist in the new list.
watch(
  () => quote.data.value,
  (current) => {
    if (!current) {
      return
    }

    if (
      shippingMethodId.value &&
      !current.shippingOptions.some((item) => item.methodId === shippingMethodId.value)
    ) {
      shippingMethodId.value = undefined
    }

    if (
      !paymentCode.value ||
      !current.paymentOptions.some((item) => item.code === paymentCode.value)
    ) {
      paymentCode.value = current.paymentOptions[0]?.code
    }
  },
)

watch(delivery, () => {
  shippingMethodId.value = undefined
})

const chosenShipping = computed(() =>
  quote.data.value?.shippingOptions.find((item) => item.methodId === shippingMethodId.value),
)
const chosenPayment = computed(() =>
  quote.data.value?.paymentOptions.find((item) => item.code === paymentCode.value),
)

const money = (amount: number, currency: string) => formatMoney({ amount, currency }, locale.value)

function deliveryDays(option: Schema<'ShippingOptionDto'>): string {
  if (option.minDays == null && option.maxDays == null) {
    return ''
  }

  return t('store.checkout.days', {
    from: option.minDays ?? 0,
    to: option.maxDays ?? option.minDays ?? 0,
  })
}

// --- Placing the order
const note = ref('')
const placing = ref(false)
const placeError = ref<string | null>(null)

/**
 * The same key for every attempt at this order, so that a retry after a lost connection or a double
 * click cannot create a second one. It changes only when the server has definitely refused.
 */
const newIdempotencyKey = () => crypto.randomUUID().replaceAll('-', '')
const idempotencyKey = ref(newIdempotencyKey())

const placedOrder = usePlacedOrder()

async function placeOrder(): Promise<void> {
  if (placing.value || !shippingMethodId.value || !paymentCode.value || !quote.data.value) {
    return
  }

  placing.value = true
  placeError.value = null

  try {
    const parsedContact = contactSchema.parse(contactForm.values)
    const isPickup = delivery.value === 'pickup'
    const newAddress =
      !isPickup && useNewAddress.value
        ? toAddressData(addressSchema.parse(addressForm.values))
        : null

    const placed = await unwrap(
      api.client.POST('/api/v1/checkout', {
        headers: { 'Idempotency-Key': idempotencyKey.value },
        body: {
          shippingMethodId: shippingMethodId.value,
          paymentMethod: paymentCode.value,
          contact: {
            name: parsedContact.name,
            phone: parsedContact.phone,
            email: parsedContact.email || null,
          },
          addressId: !isPickup && !useNewAddress.value ? (addressId.value ?? null) : null,
          address: newAddress,
          customerNote: note.value.trim() || null,
          expectedTotal: quote.data.value.totals.total,
        },
      }),
    )

    placedOrder.value = {
      number: placed.order.number,
      token: placed.trackingToken,
      instructions: placed.paymentInstructions,
    }

    await invalidate()
    queryClient.removeQueries({ queryKey: ['checkout'] })

    const redirect = safeUrl(placed.paymentRedirectUrl)

    if (redirect?.startsWith('https://')) {
      window.location.assign(redirect)

      return
    }

    await navigateTo(
      `${localePath('/order-confirmed')}#token=${encodeURIComponent(placed.trackingToken)}`,
      {
        replace: true,
      },
    )
  } catch (error) {
    placeError.value = describeError(error)

    if (error instanceof ApiError && !error.isNetworkError && error.status < 500) {
      idempotencyKey.value = newIdempotencyKey()
      await quote.refetch()
    }
  } finally {
    placing.value = false
  }
}

const isEmpty = computed(() => !cartLoading.value && !cart.value?.lines.length)
</script>

<template>
  <PageHeader :title="t('store.checkout.title')" />

  <SkeletonBlock v-if="cartLoading" :lines="6" class="max-w-3xl" />
  <EmptyState
    v-else-if="isEmpty"
    :title="t('store.cart.emptyTitle')"
    :description="t('store.cart.emptyText')"
  >
    <NuxtLink :to="localePath('/catalog')">
      <AppButton variant="primary">{{ t('store.home.browse') }}</AppButton>
    </NuxtLink>
  </EmptyState>
  <ErrorState
    v-else-if="quote.isError.value && !quote.data.value"
    :error="quote.error.value"
    retryable
    @retry="quote.refetch()"
  />

  <div v-else class="grid gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
    <div class="min-w-0">
      <el-steps :active="step" finish-status="success" align-center class="mb-6 hidden sm:flex">
        <el-step v-for="label in steps" :key="label" :title="label" />
      </el-steps>
      <p class="mb-4 text-sm font-semibold text-ink-muted sm:hidden" data-testid="step-mobile">
        {{ step + 1 }} / {{ steps.length }}: {{ steps[step] }}
      </p>

      <section
        class="rounded-card border border-line bg-surface p-4 sm:p-6"
        :aria-label="steps[step]"
      >
        <!-- Contact -->
        <AppForm
          v-if="step === 0"
          :error="contactForm.formError.value"
          @submit="contactForm.handleSubmit"
        >
          <div class="grid gap-x-4 sm:grid-cols-2">
            <AppField name="name" :label="t('store.checkout.name')" class="sm:col-span-2">
              <el-input
                v-model="contactForm.values.name"
                autocomplete="name"
                data-testid="contact-name"
              />
            </AppField>
            <AppField name="phone" :label="t('store.address.phone')">
              <el-input
                v-model="contactForm.values.phone"
                type="tel"
                autocomplete="tel"
                placeholder="+993 65 123456"
                data-testid="contact-phone"
              />
            </AppField>
            <AppField name="email" :label="t('store.checkout.emailOptional')">
              <el-input
                v-model="contactForm.values.email"
                type="email"
                autocomplete="email"
                data-testid="contact-email"
              />
            </AppField>
          </div>
          <p v-if="!auth.isAuthenticated" class="mb-4 text-sm text-ink-muted">
            {{ t('store.checkout.guestNote') }}
            <NuxtLink
              :to="localePath({ path: '/login', query: { returnUrl: localePath('/checkout') } })"
              class="font-semibold text-primary hover:underline"
            >
              {{ t('store.nav.login') }}
            </NuxtLink>
          </p>
          <AppButton type="submit" variant="primary" data-testid="next-step">
            {{ t('common.continue') }}
          </AppButton>
        </AppForm>

        <!-- Address -->
        <div v-else-if="step === 1">
          <el-radio-group v-model="delivery" class="mb-5" data-testid="delivery-kind">
            <el-radio-button value="courier">{{ t('store.checkout.deliver') }}</el-radio-button>
            <el-radio-button value="pickup">{{ t('store.checkout.pickup') }}</el-radio-button>
          </el-radio-group>

          <p v-if="delivery === 'pickup'" class="mb-5 text-sm text-ink-muted">
            {{ t('store.checkout.pickupNote') }}
          </p>

          <template v-else>
            <el-radio-group
              v-if="savedAddresses.data.value?.length"
              v-model="addressId"
              class="mb-4 !block space-y-2"
            >
              <el-radio
                v-for="saved in savedAddresses.data.value"
                :key="saved.id"
                :value="saved.id"
                border
                class="!mr-0 !flex !h-auto w-full !items-start !py-2.5"
                data-testid="saved-address"
              >
                <span class="block whitespace-normal">
                  <span class="font-semibold">{{ saved.label || saved.address.recipient }}</span>
                  <span class="block text-xs text-ink-muted">
                    {{ regionLabel(saved.address.region) }}, {{ saved.address.line1 }}
                  </span>
                </span>
              </el-radio>
              <el-radio value="new" border class="!mr-0 w-full" data-testid="new-address">
                {{ t('store.checkout.newAddress') }}
              </el-radio>
            </el-radio-group>

            <AppForm
              v-if="useNewAddress"
              :error="addressForm.formError.value"
              @submit="continueFromAddress"
            >
              <AddressFields v-model="addressForm.values" />
            </AppForm>
          </template>

          <div class="flex gap-2">
            <AppButton @click="step = 0">{{ t('common.back') }}</AppButton>
            <AppButton variant="primary" data-testid="next-step" @click="continueFromAddress">
              {{ t('common.continue') }}
            </AppButton>
          </div>
        </div>

        <!-- Shipping -->
        <div v-else-if="step === 2">
          <EmptyState
            v-if="!shippingOptions.length && !quote.isFetching.value"
            :title="t('store.checkout.noShipping')"
          />
          <el-radio-group
            v-model="shippingMethodId"
            class="mb-5 !block space-y-2"
            data-testid="shipping-options"
          >
            <el-radio
              v-for="option in shippingOptions"
              :key="option.methodId"
              :value="option.methodId"
              border
              class="!mr-0 !flex !h-auto w-full !items-start !py-2.5 [&_.el-radio\_\_label]:flex-1"
              data-testid="shipping-option"
            >
              <span class="flex w-full flex-wrap justify-between gap-x-4 whitespace-normal">
                <span>
                  <span class="font-semibold">{{ option.name }}</span>
                  <span v-if="deliveryDays(option)" class="block text-xs text-ink-muted">
                    {{ deliveryDays(option) }}
                  </span>
                  <span v-if="option.pickupAddress" class="block text-xs text-ink-muted">
                    {{ option.pickupAddress }}
                  </span>
                </span>
                <span class="font-semibold">
                  {{
                    option.price === 0
                      ? t('store.totals.free')
                      : money(option.price, option.currency)
                  }}
                </span>
              </span>
            </el-radio>
          </el-radio-group>
          <div class="flex gap-2">
            <AppButton @click="step = 1">{{ t('common.back') }}</AppButton>
            <AppButton
              variant="primary"
              :disabled="!chosenShipping"
              data-testid="next-step"
              @click="step = 3"
            >
              {{ t('common.continue') }}
            </AppButton>
          </div>
        </div>

        <!-- Payment -->
        <div v-else-if="step === 3">
          <el-radio-group v-model="paymentCode" class="mb-5 !block space-y-2">
            <el-radio
              v-for="option in quote.data.value?.paymentOptions"
              :key="option.code"
              :value="option.code"
              border
              class="!mr-0 !flex !h-auto w-full !items-start !py-2.5"
              data-testid="payment-option"
            >
              <span class="block whitespace-normal">
                <span class="font-semibold">{{ t(`enums.paymentMethod.${option.code}`) }}</span>
                <span v-if="option.instructions" class="block text-xs text-ink-muted">
                  {{ option.instructions }}
                </span>
              </span>
            </el-radio>
          </el-radio-group>
          <div class="flex gap-2">
            <AppButton @click="step = 2">{{ t('common.back') }}</AppButton>
            <AppButton
              variant="primary"
              :disabled="!chosenPayment"
              data-testid="next-step"
              @click="step = 4"
            >
              {{ t('common.continue') }}
            </AppButton>
          </div>
        </div>

        <!-- Review -->
        <div v-else>
          <dl class="mb-5 space-y-3 text-sm">
            <div>
              <dt class="text-ink-subtle">{{ t('store.checkout.steps.contact') }}</dt>
              <dd class="font-medium text-ink">
                {{ contactForm.values.name }}, {{ contactForm.values.phone }}
              </dd>
            </div>
            <div>
              <dt class="text-ink-subtle">{{ t('store.checkout.steps.shipping') }}</dt>
              <dd class="font-medium text-ink">{{ chosenShipping?.name }}</dd>
              <dd v-if="delivery === 'courier'" class="text-ink-muted">
                <template v-if="useNewAddress">
                  {{ regionLabel(addressForm.values.region) }}, {{ addressForm.values.line1 }}
                </template>
                <template v-else>
                  {{ regionLabel(chosenSaved?.address.region) }}, {{ chosenSaved?.address.line1 }}
                </template>
              </dd>
            </div>
            <div>
              <dt class="text-ink-subtle">{{ t('store.checkout.steps.payment') }}</dt>
              <dd class="font-medium text-ink">{{ t(`enums.paymentMethod.${paymentCode}`) }}</dd>
            </div>
          </dl>
          <el-form label-position="top" @submit.prevent>
            <el-form-item :label="t('store.checkout.note')">
              <el-input
                v-model="note"
                type="textarea"
                :rows="2"
                :maxlength="1000"
                show-word-limit
                data-testid="customer-note"
              />
            </el-form-item>
          </el-form>
          <el-alert
            v-if="placeError"
            :title="placeError"
            type="error"
            show-icon
            :closable="false"
            class="mb-4"
            data-testid="place-error"
          />
          <div class="flex gap-2">
            <AppButton :disabled="placing" @click="step = 3">{{ t('common.back') }}</AppButton>
            <AppButton
              variant="primary"
              size="large"
              :loading="placing"
              data-testid="place-order"
              @click="placeOrder"
            >
              {{ t('store.checkout.place') }}
            </AppButton>
          </div>
        </div>
      </section>
    </div>

    <aside>
      <div
        class="sticky top-24 rounded-card border border-line bg-surface p-5"
        data-testid="checkout-summary"
      >
        <h2 class="mb-3 font-bold text-ink">{{ t('store.checkout.summary') }}</h2>
        <ul class="mb-4 space-y-2 text-sm">
          <li
            v-for="line in quote.data.value?.cart.lines"
            :key="line.variantId"
            class="flex justify-between gap-3"
          >
            <span class="min-w-0 text-ink-muted">{{ line.productName }} × {{ line.quantity }}</span>
            <span
              v-if="line.lineTotal != null && line.currency"
              class="shrink-0 font-medium text-ink"
            >
              {{ money(line.lineTotal, line.currency) }}
            </span>
          </li>
        </ul>
        <OrderTotals
          v-if="quote.data.value"
          :totals="quote.data.value.totals"
          :shipping-known="Boolean(chosenShipping)"
        />
      </div>
    </aside>
  </div>
</template>
