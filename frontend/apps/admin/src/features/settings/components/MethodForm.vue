<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { AppField } from '@comtest/ui'
import { useQueryClient } from '@tanstack/vue-query'
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { z } from '@comtest/ui/zod'
import FormDrawer from '@/shared/components/FormDrawer.vue'
import { useAdminForm } from '@/shared/composables/useAdminForm'
import { LANGUAGES } from '@/shared/localized'
import { toastSuccess } from '@/shared/toast'
import {
  nullIfEmpty,
  optionalAmount,
  optionalText,
  requiredAmount,
  toAmount,
} from '@/shared/validation'

const props = defineProps<{
  method: Schema<'ShippingMethodDto'> | null
  zones: readonly Schema<'ShippingZoneDto'>[]
}>()
const emit = defineEmits<{ closed: [] }>()
const open = defineModel<boolean>({ required: true })

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const queryClient = useQueryClient()

const CURRENCIES = ['TMT', 'USD']
const name = optionalText(200)
const days = z.number().int().min(0).max(365).nullable()

const schema = z
  .object({
    type: z.enum(['courier', 'pickup']),
    names: z.object({ tk: name, ru: name, en: name }),
    price: requiredAmount,
    currency: z.string().min(1, 'validation.required'),
    zoneId: z.string(),
    freeOverAmount: optionalAmount,
    minDays: days,
    maxDays: days,
    pickupAddress: optionalText(500),
    sortOrder: z.number().int().min(0),
    isActive: z.boolean(),
  })
  .superRefine((values, context) => {
    if (LANGUAGES.every((language) => !values.names[language])) {
      context.addIssue({
        code: 'custom',
        path: ['names', 'tk'],
        message: 'admin.productForm.nameNeeded',
      })
    }

    if (values.type === 'courier' && !values.zoneId) {
      context.addIssue({ code: 'custom', path: ['zoneId'], message: 'validation.required' })
    }

    if (values.type === 'pickup' && !values.pickupAddress) {
      context.addIssue({ code: 'custom', path: ['pickupAddress'], message: 'validation.required' })
    }
  })

const form = useAdminForm({
  schema,
  initial: {
    type: (props.method?.type.toLowerCase() ?? 'courier') as 'courier' | 'pickup',
    names: {
      tk: props.method?.names.tk ?? '',
      ru: props.method?.names.ru ?? '',
      en: props.method?.names.en ?? '',
    },
    price: props.method ? String(props.method.price) : '',
    currency: props.method?.currency ?? 'TMT',
    zoneId: props.method?.zoneId ?? '',
    freeOverAmount:
      props.method?.freeOverAmount === null || props.method === null
        ? ''
        : String(props.method.freeOverAmount),
    minDays: props.method?.minDays ?? null,
    maxDays: props.method?.maxDays ?? null,
    pickupAddress: props.method?.pickupAddress ?? '',
    sortOrder: props.method?.sortOrder ?? 0,
    isActive: props.method?.isActive ?? true,
  },
  submit: async (values) => {
    const body: Schema<'SaveShippingMethodRequest'> = {
      type: values.type,
      names: Object.fromEntries(
        LANGUAGES.filter((language) => values.names[language]).map((language) => [
          language,
          values.names[language],
        ]),
      ),
      price: Number(values.price),
      currency: values.currency,
      zoneId: values.type === 'courier' ? values.zoneId : null,
      freeOverAmount: toAmount(values.freeOverAmount),
      minDays: values.minDays,
      maxDays: values.maxDays,
      pickupAddress: values.type === 'pickup' ? nullIfEmpty(values.pickupAddress) : null,
      sortOrder: values.sortOrder,
      isActive: values.isActive,
    }

    await unwrap(
      props.method
        ? api.client.PUT('/api/v1/admin/shipping/methods/{id}', {
            params: { path: { id: props.method.id } },
            body,
          })
        : api.client.POST('/api/v1/admin/shipping/methods', { body }),
    )

    await queryClient.invalidateQueries({ queryKey: ['admin', 'shipping'] })
    toastSuccess(t('admin.shipping.methodSaved'))
    open.value = false
  },
})

const language = ref<(typeof LANGUAGES)[number]>('tk')

const days_ = (value: number | undefined | null) => value ?? null
</script>

<template>
  <FormDrawer
    v-model="open"
    :title="t(method ? 'admin.shipping.methodEdit' : 'admin.shipping.methodNew')"
    :error="form.formError.value"
    :submitting="form.submitting.value"
    @submit="form.handleSubmit()"
    @closed="emit('closed')"
  >
    <AppField name="type" :label="t('admin.shipping.type')">
      <el-radio-group v-model="form.values.type" data-testid="method-type">
        <el-radio-button value="courier">{{ t('enums.shippingType.courier') }}</el-radio-button>
        <el-radio-button value="pickup">{{ t('enums.shippingType.pickup') }}</el-radio-button>
      </el-radio-group>
    </AppField>
    <el-radio-group v-model="language" class="mb-4" :aria-label="t('common.language')">
      <el-radio-button v-for="code in LANGUAGES" :key="code" :value="code">
        {{ t(`admin.languages.${code}`) }}
      </el-radio-button>
    </el-radio-group>
    <div v-for="code in LANGUAGES" v-show="code === language" :key="code">
      <AppField :name="`names.${code}`" :label="t('admin.common.name')">
        <el-input
          v-model="form.values.names[code]"
          maxlength="200"
          :data-testid="`method-name-${code}`"
        />
      </AppField>
    </div>
    <AppField v-if="form.values.type === 'courier'" name="zoneId" :label="t('admin.shipping.zone')">
      <el-select v-model="form.values.zoneId" data-testid="method-zone">
        <el-option v-for="zone in zones" :key="zone.id" :value="zone.id" :label="zone.name" />
      </el-select>
    </AppField>
    <AppField v-else name="pickupAddress" :label="t('admin.shipping.pickupAddress')">
      <el-input
        v-model="form.values.pickupAddress"
        type="textarea"
        :rows="2"
        maxlength="500"
        data-testid="method-pickup"
      />
    </AppField>
    <div class="grid grid-cols-2 gap-x-3">
      <AppField name="price" :label="t('admin.productForm.price')">
        <el-input v-model="form.values.price" inputmode="decimal" data-testid="method-price" />
      </AppField>
      <AppField name="currency" :label="t('admin.productForm.currency')">
        <el-select v-model="form.values.currency">
          <el-option v-for="code in CURRENCIES" :key="code" :value="code" :label="code" />
        </el-select>
      </AppField>
    </div>
    <AppField
      name="freeOverAmount"
      :label="t('admin.shipping.freeOver')"
      :hint="t('admin.shipping.freeOverHint')"
    >
      <el-input v-model="form.values.freeOverAmount" inputmode="decimal" />
    </AppField>
    <div class="grid grid-cols-2 gap-x-3">
      <AppField name="minDays" :label="t('admin.shipping.minDays')">
        <el-input-number
          :model-value="form.values.minDays ?? undefined"
          :min="0"
          :max="365"
          class="!w-full"
          @update:model-value="form.values.minDays = days_($event)"
        />
      </AppField>
      <AppField name="maxDays" :label="t('admin.shipping.maxDays')">
        <el-input-number
          :model-value="form.values.maxDays ?? undefined"
          :min="0"
          :max="365"
          class="!w-full"
          @update:model-value="form.values.maxDays = days_($event)"
        />
      </AppField>
    </div>
    <AppField name="sortOrder" :label="t('admin.categories.sortOrder')">
      <el-input-number v-model="form.values.sortOrder" :min="0" :step="10" />
    </AppField>
    <AppField name="isActive">
      <el-switch v-model="form.values.isActive" :active-text="t('admin.common.active')" />
    </AppField>
  </FormDrawer>
</template>
