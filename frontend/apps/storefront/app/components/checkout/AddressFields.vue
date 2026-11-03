<script setup lang="ts">
import { AppField } from '@comtest/ui'

export interface AddressValues {
  recipient: string
  phone: string
  region: string
  district: string
  city: string
  line1: string
  line2: string
  postalCode: string
}

const values = defineModel<AddressValues>({ required: true })

const { t } = useI18n()
const { regions, regionLabel, isPending } = useRegions()
</script>

<template>
  <div class="grid gap-x-4 sm:grid-cols-2">
    <AppField name="recipient" :label="t('store.address.recipient')">
      <el-input v-model="values.recipient" autocomplete="name" data-testid="address-recipient" />
    </AppField>
    <AppField name="phone" :label="t('store.address.phone')">
      <el-input
        v-model="values.phone"
        type="tel"
        autocomplete="tel"
        placeholder="+993 65 123456"
        data-testid="address-phone"
      />
    </AppField>
    <AppField name="region" :label="t('store.address.region')">
      <el-select
        v-model="values.region"
        :loading="isPending"
        :placeholder="t('store.address.regionPlaceholder')"
        class="w-full"
        data-testid="address-region"
      >
        <el-option v-for="code in regions" :key="code" :value="code" :label="regionLabel(code)" />
      </el-select>
    </AppField>
    <AppField name="district" :label="t('store.address.district')">
      <el-input v-model="values.district" autocomplete="address-level3" />
    </AppField>
    <AppField name="city" :label="t('store.address.city')">
      <el-input v-model="values.city" autocomplete="address-level2" />
    </AppField>
    <AppField name="postalCode" :label="t('store.address.postalCode')">
      <el-input v-model="values.postalCode" autocomplete="postal-code" />
    </AppField>
    <AppField name="line1" :label="t('store.address.line1')" class="sm:col-span-2">
      <el-input v-model="values.line1" autocomplete="address-line1" data-testid="address-line1" />
    </AppField>
    <AppField name="line2" :label="t('store.address.line2')" class="sm:col-span-2">
      <el-input v-model="values.line2" autocomplete="address-line2" />
    </AppField>
  </div>
</template>
