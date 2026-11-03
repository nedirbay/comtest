<script setup lang="ts">
import { ApiError, ErrorCode, unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { AppButton, AppField, AppForm } from '@comtest/ui'
import { useQueryClient } from '@tanstack/vue-query'
import { computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { z } from '@comtest/ui/zod'
import { useAdminForm } from '@/shared/composables/useAdminForm'
import { useUnsavedChanges } from '@/shared/composables/useUnsavedChanges'
import { toastSuccess } from '@/shared/toast'
import { nullIfEmpty, optionalText, optionalUrl, requiredText } from '@/shared/validation'

const props = defineProps<{ store: Schema<'StoreSettingsDto'> }>()
const emit = defineEmits<{ reload: [] }>()

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const queryClient = useQueryClient()

function toValues(store: Schema<'StoreSettingsDto'>) {
  return {
    name: store.name,
    logoUrl: store.logoUrl ?? '',
    contactEmail: store.contactEmail ?? '',
    contactPhone: store.contactPhone ?? '',
    address: store.address ?? '',
    workingHours: store.workingHours ?? '',
  }
}

const form = useAdminForm({
  schema: z.object({
    name: requiredText(200),
    logoUrl: optionalUrl,
    contactEmail: z
      .string()
      .trim()
      .max(254, 'validation.tooLong')
      .refine((value) => !value || z.email().safeParse(value).success, 'validation.email'),
    contactPhone: optionalText(32),
    address: optionalText(500),
    workingHours: optionalText(200),
  }),
  initial: toValues(props.store),
  submit: async (values) => {
    const saved = await unwrap(
      api.client.PUT('/api/v1/admin/store', {
        body: {
          name: values.name,
          defaultCurrency: props.store.defaultCurrency,
          defaultLanguage: props.store.defaultLanguage,
          logoUrl: nullIfEmpty(values.logoUrl),
          contactEmail: nullIfEmpty(values.contactEmail),
          contactPhone: nullIfEmpty(values.contactPhone),
          address: nullIfEmpty(values.address),
          workingHours: nullIfEmpty(values.workingHours),
          version: props.store.version,
        },
      }),
    )

    queryClient.setQueryData(['admin', 'store', 'settings'], saved)
    await queryClient.invalidateQueries({ queryKey: ['account', 'tenants'] })
    toastSuccess(t('admin.settings.saved'))
  },
})

const baseline = computed(() => JSON.stringify(toValues(props.store)))
const isDirty = computed(() => JSON.stringify(form.values) !== baseline.value)

useUnsavedChanges(isDirty)

// A save, or a reload after a conflict, brings new values from the server.
watch(
  () => props.store,
  (next) => {
    Object.assign(form.values, toValues(next))
  },
)

const isConflict = computed(
  () =>
    form.lastError.value instanceof ApiError &&
    form.lastError.value.code === ErrorCode.ConcurrencyConflict,
)
</script>

<template>
  <AppForm :error="form.formError.value" class="max-w-2xl" @submit="form.handleSubmit()">
    <el-alert
      v-if="isConflict"
      type="warning"
      :closable="false"
      class="mb-3"
      :title="t('admin.productForm.conflictText')"
    >
      <AppButton class="mt-2" @click="emit('reload')">
        {{ t('admin.productForm.conflictReload') }}
      </AppButton>
    </el-alert>
    <AppField name="name" :label="t('admin.settings.storeName')">
      <el-input v-model="form.values.name" maxlength="200" data-testid="settings-name" />
    </AppField>
    <AppField
      name="logoUrl"
      :label="t('admin.settings.logoUrl')"
      :hint="t('admin.brands.logoHint')"
    >
      <el-input v-model="form.values.logoUrl" maxlength="500" inputmode="url" />
    </AppField>
    <div class="grid gap-x-4 sm:grid-cols-2">
      <AppField name="contactEmail" :label="t('admin.settings.contactEmail')">
        <el-input v-model="form.values.contactEmail" maxlength="254" inputmode="email" />
      </AppField>
      <AppField name="contactPhone" :label="t('admin.settings.contactPhone')">
        <el-input v-model="form.values.contactPhone" maxlength="32" inputmode="tel" />
      </AppField>
    </div>
    <AppField name="address" :label="t('admin.settings.address')">
      <el-input v-model="form.values.address" type="textarea" :rows="2" maxlength="500" />
    </AppField>
    <AppField name="workingHours" :label="t('admin.settings.workingHours')">
      <el-input v-model="form.values.workingHours" maxlength="200" />
    </AppField>
    <AppButton
      variant="primary"
      type="submit"
      :loading="form.submitting.value"
      data-testid="settings-save"
    >
      {{ t('common.save') }}
    </AppButton>

    <dl class="mt-6 grid gap-3 border-t border-line pt-4 text-sm sm:grid-cols-4">
      <div>
        <dt class="text-xs text-ink-muted">{{ t('admin.settings.plan') }}</dt>
        <dd class="font-semibold text-ink">{{ store.plan }}</dd>
      </div>
      <div>
        <dt class="text-xs text-ink-muted">{{ t('admin.settings.maxProducts') }}</dt>
        <dd class="font-semibold tabular-nums text-ink">{{ store.maxProducts }}</dd>
      </div>
      <div>
        <dt class="text-xs text-ink-muted">{{ t('admin.settings.maxStaff') }}</dt>
        <dd class="font-semibold tabular-nums text-ink">{{ store.maxStaff }}</dd>
      </div>
      <div>
        <dt class="text-xs text-ink-muted">{{ t('admin.settings.storage') }}</dt>
        <dd class="font-semibold tabular-nums text-ink">{{ store.storageQuotaMegabytes }} MB</dd>
      </div>
    </dl>
  </AppForm>
</template>
