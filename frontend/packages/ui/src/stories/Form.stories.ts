import type { Meta, StoryObj } from '@storybook/vue3-vite'
import { useI18n } from 'vue-i18n'
import { z } from 'zod'
import AppButton from '../components/AppButton.vue'
import AppField from '../components/AppField.vue'
import AppForm from '../components/AppForm.vue'
import { useApiErrorMessage } from '../composables/useApiErrorMessage'
import { useForm } from '../composables/useForm'

const meta: Meta = { title: 'Elements/AppForm' }

export default meta

const schema = z.object({
  email: z.email('validation.email'),
  password: z.string().min(12, 'validation.passwordLength'),
})

/** Submit it empty to see the validation messages in the chosen language. */
export const WithValidation: StoryObj = {
  render: () => ({
    components: { AppButton, AppField, AppForm },
    setup() {
      const { t } = useI18n({ useScope: 'global' })
      const describeError = useApiErrorMessage()
      const form = useForm({
        schema,
        initial: { email: '', password: '' },
        translate: (key) => t(key),
        describeError,
        submit: () => Promise.resolve(),
      })

      return { form }
    },
    template: `
      <AppForm class="max-w-md" :error="form.formError.value" @submit="form.handleSubmit">
        <AppField name="email" label="E-poçta">
          <el-input v-model="form.values.email" />
        </AppField>
        <AppField name="password" label="Açar sözi" hint="Azyndan 12 belgi">
          <el-input v-model="form.values.password" type="password" show-password />
        </AppField>
        <AppButton type="submit" variant="primary" :loading="form.submitting.value">Sakla</AppButton>
      </AppForm>
    `,
  }),
}

export const WithServerError: StoryObj = {
  render: () => ({
    components: { AppButton, AppField, AppForm },
    template: `
      <AppForm class="max-w-md" error="Maglumatlary saklap bolmady.">
        <AppField name="email" label="E-poçta"><el-input /></AppField>
        <AppButton variant="primary">Sakla</AppButton>
      </AppForm>
    `,
  }),
}
