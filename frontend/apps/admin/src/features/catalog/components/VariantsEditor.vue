<script setup lang="ts">
import { AppButton, AppField } from '@comtest/ui'
import { Delete, Plus } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'
import { useAuthStore } from '@comtest/auth'
import { Permission } from '@/app/permissions'
import { emptyVariant, type VariantValues } from '../productForm'

const props = defineProps<{ currency: string; disabled?: boolean }>()
const variants = defineModel<VariantValues[]>({ required: true })

const { t } = useI18n({ useScope: 'global' })
const auth = useAuthStore()

const CURRENCIES = ['TMT', 'USD']

function add(): void {
  variants.value.push(emptyVariant(props.currency))
}

function remove(index: number): void {
  variants.value.splice(index, 1)
}
</script>

<template>
  <div class="w-full space-y-4">
    <article
      v-for="(variant, index) in variants"
      :key="variant.id ?? index"
      class="rounded-card border border-line p-4"
      data-testid="variant"
    >
      <div class="mb-2 flex items-center justify-between gap-2">
        <h3 class="font-semibold text-ink">
          {{ t('admin.productForm.variantTitle', { number: index + 1 }) }}
        </h3>
        <div class="flex items-center gap-3">
          <RouterLink
            v-if="variant.id && auth.can(Permission.InventoryRead)"
            :to="{ name: 'stock', query: { variantId: variant.id } }"
            class="text-sm font-semibold text-primary"
            data-testid="variant-stock-link"
          >
            {{ t('admin.productForm.seeStock') }}
          </RouterLink>
          <el-switch
            v-model="variant.isActive"
            :active-text="t('admin.common.active')"
            :disabled="disabled"
          />
          <el-button
            v-if="!disabled && variants.length > 1"
            :icon="Delete"
            type="danger"
            plain
            size="small"
            :aria-label="t('common.delete')"
            data-testid="variant-remove"
            @click="remove(index)"
          />
        </div>
      </div>
      <div class="grid gap-x-3 sm:grid-cols-2 lg:grid-cols-3">
        <AppField :name="`variants.${index}.sku`" :label="t('admin.productForm.sku')">
          <el-input v-model="variant.sku" maxlength="100" data-testid="variant-sku" />
        </AppField>
        <AppField :name="`variants.${index}.barcode`" :label="t('admin.productForm.barcode')">
          <el-input v-model="variant.barcode" maxlength="50" data-testid="variant-barcode" />
        </AppField>
        <AppField :name="`variants.${index}.currency`" :label="t('admin.productForm.currency')">
          <el-select v-model="variant.currency" data-testid="variant-currency">
            <el-option v-for="code in CURRENCIES" :key="code" :value="code" :label="code" />
          </el-select>
        </AppField>
        <AppField :name="`variants.${index}.price`" :label="t('admin.productForm.price')">
          <el-input v-model="variant.price" inputmode="decimal" data-testid="variant-price" />
        </AppField>
        <AppField :name="`variants.${index}.compareAt`" :label="t('admin.productForm.oldPrice')">
          <el-input
            v-model="variant.compareAt"
            inputmode="decimal"
            data-testid="variant-old-price"
          />
        </AppField>
        <AppField :name="`variants.${index}.cost`" :label="t('admin.productForm.cost')">
          <el-input v-model="variant.cost" inputmode="decimal" data-testid="variant-cost" />
        </AppField>
      </div>

      <p class="mb-1 text-sm font-semibold text-ink">{{ t('admin.productForm.options') }}</p>
      <div
        v-for="(option, optionIndex) in variant.options"
        :key="optionIndex"
        class="mb-2 flex items-center gap-2"
      >
        <el-input
          v-model="option.key"
          :placeholder="t('admin.productForm.optionName')"
          :aria-label="t('admin.productForm.optionName')"
          maxlength="100"
          data-testid="option-key"
        />
        <el-input
          v-model="option.value"
          :placeholder="t('admin.productForm.optionValue')"
          :aria-label="t('admin.productForm.optionValue')"
          maxlength="200"
          data-testid="option-value"
        />
        <el-button
          v-if="!disabled"
          :icon="Delete"
          :aria-label="t('common.delete')"
          @click="variant.options.splice(optionIndex, 1)"
        />
      </div>
      <el-button
        v-if="!disabled && variant.options.length < 10"
        :icon="Plus"
        text
        size="small"
        data-testid="option-add"
        @click="variant.options.push({ key: '', value: '' })"
      >
        {{ t('admin.productForm.addOption') }}
      </el-button>
    </article>
    <AppButton v-if="!disabled" data-testid="variant-add" @click="add">
      {{ t('admin.productForm.addVariant') }}
    </AppButton>
  </div>
</template>
