<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { AppField } from '@comtest/ui'
import { useQueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { z } from '@comtest/ui/zod'
import FormDrawer from '@/shared/components/FormDrawer.vue'
import { useAdminForm } from '@/shared/composables/useAdminForm'
import { toastSuccess } from '@/shared/toast'
import { nullIfEmpty, optionalText, requiredText } from '@/shared/validation'
import { WAREHOUSES_KEY } from '../queries'

const props = defineProps<{ warehouse: Schema<'WarehouseDto'> | null; readonly?: boolean }>()
const emit = defineEmits<{ closed: [] }>()
const open = defineModel<boolean>({ required: true })

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const queryClient = useQueryClient()

const form = useAdminForm({
  schema: z.object({
    name: requiredText(200),
    code: requiredText(50).regex(/^[A-Za-z0-9_-]+$/, 'admin.warehouses.codeInvalid'),
    address: optionalText(500),
    isDefault: z.boolean(),
    isActive: z.boolean(),
  }),
  initial: {
    name: props.warehouse?.name ?? '',
    code: props.warehouse?.code ?? '',
    address: props.warehouse?.address ?? '',
    isDefault: props.warehouse?.isDefault ?? false,
    isActive: props.warehouse?.isActive ?? true,
  },
  submit: async (values) => {
    const body = { ...values, address: nullIfEmpty(values.address) }

    await unwrap(
      props.warehouse
        ? api.client.PUT('/api/v1/admin/inventory/warehouses/{id}', {
            params: { path: { id: props.warehouse.id } },
            body,
          })
        : api.client.POST('/api/v1/admin/inventory/warehouses', { body }),
    )

    await queryClient.invalidateQueries({ queryKey: WAREHOUSES_KEY })
    toastSuccess(t('admin.warehouses.saved'))
    open.value = false
  },
})
</script>

<template>
  <FormDrawer
    v-model="open"
    :title="t(warehouse ? 'admin.warehouses.edit' : 'admin.warehouses.new')"
    :error="form.formError.value"
    :submitting="form.submitting.value"
    :readonly="readonly"
    @submit="form.handleSubmit()"
    @closed="emit('closed')"
  >
    <AppField name="name" :label="t('admin.common.name')">
      <el-input v-model="form.values.name" maxlength="200" data-testid="warehouse-name" />
    </AppField>
    <AppField
      name="code"
      :label="t('admin.warehouses.code')"
      :hint="t('admin.warehouses.codeHint')"
    >
      <el-input v-model="form.values.code" maxlength="50" data-testid="warehouse-code" />
    </AppField>
    <AppField name="address" :label="t('admin.warehouses.address')">
      <el-input v-model="form.values.address" type="textarea" :rows="2" maxlength="500" />
    </AppField>
    <AppField name="isDefault">
      <el-switch v-model="form.values.isDefault" :active-text="t('admin.warehouses.isDefault')" />
    </AppField>
    <AppField name="isActive">
      <el-switch v-model="form.values.isActive" :active-text="t('admin.common.active')" />
    </AppField>
  </FormDrawer>
</template>
