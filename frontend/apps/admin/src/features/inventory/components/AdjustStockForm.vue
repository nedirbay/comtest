<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { AppField } from '@comtest/ui'
import { useQuery, useQueryClient } from '@tanstack/vue-query'
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { z } from '@comtest/ui/zod'
import { Permission } from '@/app/permissions'
import { useAuthStore } from '@comtest/auth'
import FormDrawer from '@/shared/components/FormDrawer.vue'
import { useAdminForm } from '@/shared/composables/useAdminForm'
import { toastSuccess } from '@/shared/toast'
import { requiredText } from '@/shared/validation'
import { MOVEMENTS_KEY, STOCK_KEY, useWarehouses } from '../queries'

const MAX_DELTA = 1_000_000
const PRODUCT_CHOICES = 100

const props = defineProps<{
  /** Set when adjusting one row of the list. Otherwise the user picks product and variant. */
  stock: Schema<'StockItemDto'> | null
}>()
const emit = defineEmits<{ closed: [] }>()
const open = defineModel<boolean>({ required: true })

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const queryClient = useQueryClient()
const warehouses = useWarehouses()

const canPickProduct = computed(() => auth.can(Permission.CatalogRead))

const schema = z
  .object({
    warehouseId: z.string().min(1, 'validation.required'),
    variantId: z.string().min(1, 'validation.required'),
    type: z.enum(['receipt', 'adjustment']),
    delta: z
      .string()
      .trim()
      .regex(/^[+-]?\d{1,7}$/, 'admin.stock.deltaInvalid'),
    reason: requiredText(500),
  })
  .superRefine((values, context) => {
    const delta = Number(values.delta)

    if (delta === 0 || Math.abs(delta) > MAX_DELTA) {
      context.addIssue({ code: 'custom', path: ['delta'], message: 'admin.stock.deltaInvalid' })
    } else if (values.type === 'receipt' && delta < 0) {
      context.addIssue({ code: 'custom', path: ['delta'], message: 'admin.stock.receiptPositive' })
    }
  })

const form = useAdminForm({
  schema,
  initial: {
    warehouseId: props.stock?.warehouseId ?? '',
    variantId: props.stock?.variantId ?? '',
    type: 'receipt' as 'receipt' | 'adjustment',
    delta: '',
    reason: '',
  },
  submit: async (values) => {
    await unwrap(
      api.client.POST('/api/v1/admin/inventory/stock/adjustments', {
        body: {
          warehouseId: values.warehouseId,
          variantId: values.variantId,
          type: values.type,
          delta: Number(values.delta),
          reason: values.reason,
        },
      }),
    )

    await queryClient.invalidateQueries({ queryKey: STOCK_KEY })
    await queryClient.invalidateQueries({ queryKey: MOVEMENTS_KEY })
    toastSuccess(t('admin.stock.adjusted'))
    open.value = false
  },
})

watch(
  () => warehouses.data.value,
  (list) => {
    if (!form.values.warehouseId && list?.length) {
      form.values.warehouseId = list.find((item) => item.isDefault)?.id ?? list[0]?.id ?? ''
    }
  },
  { immediate: true },
)

const productId = ref('')

const products = useQuery({
  queryKey: ['admin', 'catalog', 'products', 'picker'],
  enabled: computed(() => !props.stock && canPickProduct.value),
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/catalog/products', {
        params: { query: { pageSize: PRODUCT_CHOICES, sort: '-createdAt' } },
      }),
    ),
})

const chosenProduct = useQuery({
  queryKey: ['admin', 'catalog', 'products', 'detail', productId],
  enabled: computed(() => productId.value !== ''),
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/catalog/products/{id}', {
        params: { path: { id: productId.value } },
      }),
    ),
})

watch(productId, () => {
  form.values.variantId = ''
})

watch(
  () => chosenProduct.data.value,
  (product) => {
    if (product?.id === productId.value && product.variants.length === 1) {
      form.values.variantId = product.variants[0]?.id ?? ''
    }
  },
)

function variantLabel(variant: Schema<'VariantAdminDto'>): string {
  const options = Object.values(variant.options).join(', ')

  return options ? `${variant.sku} (${options})` : variant.sku
}
</script>

<template>
  <FormDrawer
    v-model="open"
    :title="t('admin.stock.adjust')"
    :error="form.formError.value"
    :submitting="form.submitting.value"
    @submit="form.handleSubmit()"
    @closed="emit('closed')"
  >
    <p v-if="stock" class="mb-3 text-sm text-ink">
      <span class="font-semibold">{{ stock.productName }}</span>
      <span class="text-ink-muted">· {{ stock.sku }} · {{ stock.warehouseCode }}</span>
    </p>
    <template v-else>
      <AppField name="warehouseId" :label="t('admin.stock.warehouse')">
        <el-select v-model="form.values.warehouseId" data-testid="adjust-warehouse">
          <el-option
            v-for="warehouse in warehouses.data.value"
            :key="warehouse.id"
            :value="warehouse.id"
            :label="`${warehouse.name} (${warehouse.code})`"
          />
        </el-select>
      </AppField>
      <AppField
        name="productId"
        :label="t('admin.stock.product')"
        :hint="t('admin.stock.productHint')"
      >
        <el-select v-model="productId" filterable data-testid="adjust-product">
          <el-option
            v-for="product in products.data.value?.items"
            :key="product.id"
            :value="product.id"
            :label="product.name ?? product.slug"
          />
        </el-select>
      </AppField>
      <AppField name="variantId" :label="t('admin.stock.variant')">
        <el-select
          v-model="form.values.variantId"
          :disabled="!productId"
          data-testid="adjust-variant"
        >
          <el-option
            v-for="variant in chosenProduct.data.value?.variants"
            :key="variant.id"
            :value="variant.id"
            :label="variantLabel(variant)"
          />
        </el-select>
      </AppField>
    </template>
    <AppField name="type" :label="t('admin.stock.type')">
      <el-radio-group v-model="form.values.type" data-testid="adjust-type">
        <el-radio-button value="receipt">{{ t('admin.stock.typeReceipt') }}</el-radio-button>
        <el-radio-button value="adjustment">{{ t('admin.stock.typeAdjustment') }}</el-radio-button>
      </el-radio-group>
    </AppField>
    <AppField name="delta" :label="t('admin.stock.delta')" :hint="t('admin.stock.deltaHint')">
      <el-input v-model="form.values.delta" inputmode="numeric" data-testid="adjust-delta" />
    </AppField>
    <AppField name="reason" :label="t('admin.stock.reason')">
      <el-input
        v-model="form.values.reason"
        type="textarea"
        :rows="2"
        maxlength="500"
        data-testid="adjust-reason"
      />
    </AppField>
  </FormDrawer>
</template>
