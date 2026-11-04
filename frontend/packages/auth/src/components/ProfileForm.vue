<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { AppButton, AppField, AppForm } from '@comtest/ui'
import { LOCALE_NAMES, SUPPORTED_LOCALES } from '@comtest/ui/locale'
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { profileSchema } from '../schemas'
import { useApi, useAuthStore } from '../store'
import { useAuthForm } from '../useAuthForm'

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const saved = ref(false)

const form = useAuthForm({
  schema: profileSchema,
  initial: { fullName: auth.profile?.fullName ?? '', locale: auth.profile?.locale ?? 'tk' },
  submit: async (values) => {
    saved.value = false
    auth.profile = await unwrap(api.client.PUT('/api/v1/account/me', { body: values }))
    saved.value = true
  },
})
</script>

<template>
  <AppForm :error="form.formError.value" class="max-w-md" @submit="form.handleSubmit">
    <el-alert
      v-if="saved"
      :title="t('auth.profile.saved')"
      type="success"
      show-icon
      :closable="false"
      class="mb-4"
      data-testid="profile-saved"
    />
    <AppField name="fullName" :label="t('auth.fields.fullName')">
      <el-input
        v-model="form.values.fullName"
        autocomplete="name"
        maxlength="200"
        data-testid="profile-name"
      />
    </AppField>
    <AppField name="locale" :label="t('auth.profile.locale')" :hint="t('auth.profile.localeHint')">
      <el-select v-model="form.values.locale" data-testid="profile-locale">
        <el-option
          v-for="locale in SUPPORTED_LOCALES"
          :key="locale"
          :value="locale"
          :label="LOCALE_NAMES[locale]"
        />
      </el-select>
    </AppField>
    <AppButton
      type="submit"
      variant="primary"
      :loading="form.submitting.value"
      data-testid="profile-save"
    >
      {{ t('common.save') }}
    </AppButton>
  </AppForm>
</template>
