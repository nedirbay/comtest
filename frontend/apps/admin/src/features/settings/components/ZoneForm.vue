<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { AppField, REGION_CODES } from '@comtest/ui'
import { useQueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { z } from '@comtest/ui/zod'
import FormDrawer from '@/shared/components/FormDrawer.vue'
import { useAdminForm } from '@/shared/composables/useAdminForm'
import { toastSuccess } from '@/shared/toast'
import { requiredText } from '@/shared/validation'

const props = defineProps<{ zone: Schema<'ShippingZoneDto'> | null }>()
const emit = defineEmits<{ closed: [] }>()
const open = defineModel<boolean>({ required: true })

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const queryClient = useQueryClient()

const form = useAdminForm({
  schema: z.object({
    name: requiredText(200),
    regions: z.array(z.string()).min(1, 'admin.shipping.regionsNeeded'),
    isActive: z.boolean(),
  }),
  initial: {
    name: props.zone?.name ?? '',
    regions: [...(props.zone?.regions ?? [])],
    isActive: props.zone?.isActive ?? true,
  },
  submit: async (values) => {
    await unwrap(
      props.zone
        ? api.client.PUT('/api/v1/admin/shipping/zones/{id}', {
            params: { path: { id: props.zone.id } },
            body: values,
          })
        : api.client.POST('/api/v1/admin/shipping/zones', { body: values }),
    )

    await queryClient.invalidateQueries({ queryKey: ['admin', 'shipping'] })
    toastSuccess(t('admin.shipping.zoneSaved'))
    open.value = false
  },
})
</script>

<template>
  <FormDrawer
    v-model="open"
    :title="t(zone ? 'admin.shipping.zoneEdit' : 'admin.shipping.zoneNew')"
    :error="form.formError.value"
    :submitting="form.submitting.value"
    @submit="form.handleSubmit()"
    @closed="emit('closed')"
  >
    <AppField name="name" :label="t('admin.common.name')">
      <el-input v-model="form.values.name" maxlength="200" data-testid="zone-name" />
    </AppField>
    <AppField
      name="regions"
      :label="t('admin.shipping.regions')"
      :hint="t('admin.shipping.regionsHint')"
    >
      <el-select
        v-model="form.values.regions"
        multiple
        filterable
        allow-create
        default-first-option
        :reserve-keyword="false"
        data-testid="zone-regions"
      >
        <el-option
          v-for="code in REGION_CODES"
          :key="code"
          :value="code"
          :label="t(`regions.${code}`)"
        />
      </el-select>
    </AppField>
    <AppField name="isActive">
      <el-switch v-model="form.values.isActive" :active-text="t('admin.common.active')" />
    </AppField>
  </FormDrawer>
</template>
