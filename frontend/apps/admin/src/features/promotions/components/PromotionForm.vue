<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { AppField } from '@comtest/ui'
import { Refresh } from '@element-plus/icons-vue'
import { useQueryClient } from '@tanstack/vue-query'
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { z } from '@comtest/ui/zod'
import { useCategories } from '@/features/catalog/queries'
import FormDrawer from '@/shared/components/FormDrawer.vue'
import { useAdminForm } from '@/shared/composables/useAdminForm'
import { toastSuccess } from '@/shared/toast'
import { nullIfEmpty, optionalAmount, requiredText, toAmount } from '@/shared/validation'

type Promotion = Schema<'PromotionDto'>
type PromotionType = Schema<'PromotionType'>

const props = defineProps<{ promotion: Promotion | null }>()
const emit = defineEmits<{ closed: [] }>()
const open = defineModel<boolean>({ required: true })

const TYPES: PromotionType[] = ['percentage', 'fixedAmount', 'freeShipping']
const CODE_PATTERN = /^[A-Za-z0-9-]{6,32}$/

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const queryClient = useQueryClient()
const categories = useCategories()

const automatic = ref(Boolean(props.promotion) && props.promotion?.code === null)

const countField = z
  .string()
  .trim()
  .refine((value) => value === '' || /^[1-9]\d{0,8}$/.test(value), 'admin.validation.amount')

const form = useAdminForm({
  schema: z
    .object({
      code: z.string().trim(),
      name: requiredText(200),
      type: z.string(),
      value: optionalAmount,
      minSubtotal: optionalAmount,
      categoryId: z.string(),
      startsAt: z.string().nullable(),
      endsAt: z.string().nullable(),
      usageLimit: countField,
      perCustomerLimit: countField,
      combinesWithOthers: z.boolean(),
      isActive: z.boolean(),
    })
    .refine((values) => automatic.value || CODE_PATTERN.test(values.code), {
      path: ['code'],
      message: 'admin.promotions.codeRule',
    })
    .refine((values) => values.type === 'freeShipping' || (toAmount(values.value) ?? 0) > 0, {
      path: ['value'],
      message: 'admin.validation.amount',
    })
    .refine((values) => values.type !== 'percentage' || (toAmount(values.value) ?? 0) <= 100, {
      path: ['value'],
      message: 'admin.promotions.percentRule',
    })
    .refine((values) => !values.startsAt || !values.endsAt || values.endsAt > values.startsAt, {
      path: ['endsAt'],
      message: 'admin.promotions.periodRule',
    }),
  initial: {
    code: props.promotion?.code ?? '',
    name: props.promotion?.name ?? '',
    type: props.promotion?.type ?? ('percentage' as string),
    value: props.promotion?.value ? String(props.promotion.value) : '',
    minSubtotal: props.promotion?.minSubtotal ? String(props.promotion.minSubtotal) : '',
    categoryId: props.promotion?.categoryId ?? '',
    startsAt: (props.promotion?.startsAt ?? null) as string | null,
    endsAt: (props.promotion?.endsAt ?? null) as string | null,
    usageLimit: props.promotion?.usageLimit ? String(props.promotion.usageLimit) : '',
    perCustomerLimit: props.promotion?.perCustomerLimit
      ? String(props.promotion.perCustomerLimit)
      : '',
    combinesWithOthers: props.promotion?.combinesWithOthers ?? false,
    isActive: props.promotion?.isActive ?? true,
  },
  submit: async (values) => {
    const body: Schema<'SavePromotionRequest'> = {
      code: automatic.value ? null : values.code,
      name: values.name,
      type: values.type as PromotionType,
      value: values.type === 'freeShipping' ? 0 : (toAmount(values.value) ?? 0),
      minSubtotal: toAmount(values.minSubtotal),
      categoryId: nullIfEmpty(values.categoryId),
      startsAt: values.startsAt,
      endsAt: values.endsAt,
      usageLimit: toAmount(values.usageLimit),
      perCustomerLimit: toAmount(values.perCustomerLimit),
      combinesWithOthers: values.combinesWithOthers,
      isActive: values.isActive,
    }

    await unwrap(
      props.promotion
        ? api.client.PUT('/api/v1/admin/promotions/{id}', {
            params: { path: { id: props.promotion.id } },
            body,
          })
        : api.client.POST('/api/v1/admin/promotions', { body }),
    )

    await queryClient.invalidateQueries({ queryKey: ['admin', 'promotions'] })
    toastSuccess(t('admin.promotions.saved'))
    open.value = false
  },
})

const hasValue = computed(() => form.values.type !== 'freeShipping')
const valueLabel = computed(() =>
  t(form.values.type === 'percentage' ? 'admin.promotions.percent' : 'admin.promotions.amount'),
)

async function generateCode(): Promise<void> {
  const created = await unwrap(api.client.GET('/api/v1/admin/promotions/new-code'))

  form.values.code = created.code
}
</script>

<template>
  <FormDrawer
    v-model="open"
    :title="t(promotion ? 'admin.promotions.edit' : 'admin.promotions.new')"
    :error="form.formError.value"
    :submitting="form.submitting.value"
    @submit="form.handleSubmit()"
    @closed="emit('closed')"
  >
    <AppField name="name" :label="t('admin.common.name')">
      <el-input v-model="form.values.name" maxlength="200" data-testid="promotion-name" />
    </AppField>

    <el-form-item :label="t('admin.promotions.howApplied')">
      <el-radio-group v-model="automatic" data-testid="promotion-kind">
        <el-radio :value="false">{{ t('admin.promotions.withCode') }}</el-radio>
        <el-radio :value="true" data-testid="promotion-automatic">
          {{ t('admin.promotions.automatic') }}
        </el-radio>
      </el-radio-group>
    </el-form-item>

    <AppField
      v-if="!automatic"
      name="code"
      :label="t('admin.promotions.code')"
      :hint="t('admin.promotions.codeHint')"
    >
      <div class="flex w-full gap-2">
        <el-input
          v-model="form.values.code"
          maxlength="32"
          class="uppercase"
          data-testid="promotion-code"
        />
        <el-button :icon="Refresh" data-testid="promotion-generate" @click="generateCode">
          {{ t('admin.promotions.generate') }}
        </el-button>
      </div>
    </AppField>

    <AppField name="type" :label="t('admin.promotions.type')">
      <el-select v-model="form.values.type" class="w-full" data-testid="promotion-type">
        <el-option
          v-for="type in TYPES"
          :key="type"
          :value="type"
          :label="t(`admin.promotions.types.${type}`)"
        />
      </el-select>
    </AppField>

    <AppField v-if="hasValue" name="value" :label="valueLabel">
      <el-input v-model="form.values.value" inputmode="decimal" data-testid="promotion-value" />
    </AppField>

    <AppField name="minSubtotal" :label="t('admin.promotions.minSubtotal')">
      <el-input v-model="form.values.minSubtotal" inputmode="decimal" />
    </AppField>

    <AppField
      name="categoryId"
      :label="t('admin.promotions.category')"
      :hint="t('admin.promotions.categoryHint')"
    >
      <el-select v-model="form.values.categoryId" class="w-full" clearable filterable>
        <el-option
          v-for="category in categories.data.value"
          :key="category.id"
          :value="category.id"
          :label="category.translations[0]?.name ?? category.slug"
        />
      </el-select>
    </AppField>

    <div class="grid gap-3 sm:grid-cols-2">
      <AppField name="startsAt" :label="t('admin.promotions.startsAt')">
        <el-date-picker
          v-model="form.values.startsAt"
          class="!w-full"
          type="datetime"
          value-format="YYYY-MM-DDTHH:mm:ssZ"
          :aria-label="t('admin.promotions.startsAt')"
        />
      </AppField>
      <AppField name="endsAt" :label="t('admin.promotions.endsAt')">
        <el-date-picker
          v-model="form.values.endsAt"
          class="!w-full"
          type="datetime"
          value-format="YYYY-MM-DDTHH:mm:ssZ"
          :aria-label="t('admin.promotions.endsAt')"
        />
      </AppField>
    </div>

    <div class="grid gap-3 sm:grid-cols-2">
      <AppField
        name="usageLimit"
        :label="t('admin.promotions.usageLimit')"
        :hint="t('admin.promotions.limitHint')"
      >
        <el-input v-model="form.values.usageLimit" inputmode="numeric" />
      </AppField>
      <AppField
        name="perCustomerLimit"
        :label="t('admin.promotions.perCustomerLimit')"
        :hint="t('admin.promotions.limitHint')"
      >
        <el-input v-model="form.values.perCustomerLimit" inputmode="numeric" />
      </AppField>
    </div>

    <AppField name="combinesWithOthers" :hint="t('admin.promotions.combinesHint')">
      <el-switch
        v-model="form.values.combinesWithOthers"
        :active-text="t('admin.promotions.combines')"
        :aria-label="t('admin.promotions.combines')"
      />
    </AppField>
    <AppField name="isActive">
      <el-switch
        v-model="form.values.isActive"
        :active-text="t('admin.promotions.active')"
        :aria-label="t('admin.promotions.active')"
        data-testid="promotion-active"
      />
    </AppField>
  </FormDrawer>
</template>
