<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { AppButton, AppField, AppForm } from '@comtest/ui'
import { useQueryClient } from '@tanstack/vue-query'
import { computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { z } from '@comtest/ui/zod'
import { useAdminForm } from '@/shared/composables/useAdminForm'
import { useUnsavedChanges } from '@/shared/composables/useUnsavedChanges'
import { LANGUAGES } from '@/shared/localized'
import { toastSuccess } from '@/shared/toast'

const props = defineProps<{ store: Schema<'StoreSettingsDto'> }>()

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const queryClient = useQueryClient()

const CURRENCIES = ['TMT', 'USD']

function toValues(store: Schema<'StoreSettingsDto'>) {
  return { defaultLanguage: store.defaultLanguage, defaultCurrency: store.defaultCurrency }
}

const form = useAdminForm({
  schema: z.object({
    defaultLanguage: z.string().min(1, 'validation.required'),
    defaultCurrency: z.string().min(1, 'validation.required'),
  }),
  initial: toValues(props.store),
  submit: async (values) => {
    const saved = await unwrap(
      api.client.PUT('/api/v1/admin/store', {
        body: {
          name: props.store.name,
          logoUrl: props.store.logoUrl,
          contactEmail: props.store.contactEmail,
          contactPhone: props.store.contactPhone,
          address: props.store.address,
          workingHours: props.store.workingHours,
          version: props.store.version,
          ...values,
        },
      }),
    )

    queryClient.setQueryData(['admin', 'store', 'settings'], saved)
    await queryClient.invalidateQueries({ queryKey: ['admin', 'store', 'public'] })
    toastSuccess(t('admin.settings.saved'))
  },
})

const isDirty = computed(
  () => JSON.stringify(form.values) !== JSON.stringify(toValues(props.store)),
)

useUnsavedChanges(isDirty)

watch(
  () => props.store,
  (next) => {
    Object.assign(form.values, toValues(next))
  },
)
</script>

<template>
  <AppForm :error="form.formError.value" class="max-w-xl" @submit="form.handleSubmit()">
    <p class="mb-4 text-sm text-ink-muted">{{ t('admin.settings.languagesIntro') }}</p>
    <AppField name="defaultLanguage" :label="t('admin.settings.defaultLanguage')">
      <el-select v-model="form.values.defaultLanguage" data-testid="settings-language">
        <el-option
          v-for="code in LANGUAGES"
          :key="code"
          :value="code"
          :label="t(`admin.languages.${code}`)"
        />
      </el-select>
    </AppField>
    <AppField
      name="defaultCurrency"
      :label="t('admin.settings.defaultCurrency')"
      :hint="t('admin.settings.currencyHint')"
    >
      <el-select v-model="form.values.defaultCurrency" data-testid="settings-currency">
        <el-option v-for="code in CURRENCIES" :key="code" :value="code" :label="code" />
      </el-select>
    </AppField>
    <AppButton
      variant="primary"
      type="submit"
      :loading="form.submitting.value"
      data-testid="settings-language-save"
    >
      {{ t('common.save') }}
    </AppButton>
  </AppForm>
</template>
