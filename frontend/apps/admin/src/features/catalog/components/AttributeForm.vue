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
import { optionalText, slugField } from '@/shared/validation'
import { ATTRIBUTES_KEY } from '../queries'

const emit = defineEmits<{ closed: [] }>()
const open = defineModel<boolean>({ required: true })

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const queryClient = useQueryClient()

const DATA_TYPES: Schema<'AttributeDataType'>[] = ['text', 'number', 'boolean', 'select']

const name = optionalText(300)

const schema = z
  .object({
    code: slugField,
    dataType: z.enum(['text', 'number', 'boolean', 'select']),
    options: z.array(z.string()),
    names: z.object({ tk: name, ru: name, en: name }),
  })
  .superRefine((values, context) => {
    if (LANGUAGES.every((language) => !values.names[language])) {
      context.addIssue({
        code: 'custom',
        path: ['names', 'tk'],
        message: 'admin.productForm.nameNeeded',
      })
    }

    if (values.dataType === 'select' && values.options.length === 0) {
      context.addIssue({
        code: 'custom',
        path: ['options'],
        message: 'admin.attributes.optionsNeeded',
      })
    }
  })

const form = useAdminForm({
  schema,
  initial: {
    code: '',
    dataType: 'text' as Schema<'AttributeDataType'>,
    options: [] as string[],
    names: { tk: '', ru: '', en: '' },
  },
  submit: async (values) => {
    await unwrap(
      api.client.POST('/api/v1/admin/catalog/attributes', {
        body: {
          code: values.code,
          dataType: values.dataType,
          options: values.dataType === 'select' ? values.options : null,
          translations: LANGUAGES.filter((language) => values.names[language]).map((language) => ({
            language,
            name: values.names[language],
          })),
        },
      }),
    )

    await queryClient.invalidateQueries({ queryKey: ATTRIBUTES_KEY })
    toastSuccess(t('admin.attributes.saved'))
    open.value = false
  },
})

const language = ref<(typeof LANGUAGES)[number]>('tk')
</script>

<template>
  <FormDrawer
    v-model="open"
    :title="t('admin.attributes.new')"
    :error="form.formError.value"
    :submitting="form.submitting.value"
    @submit="form.handleSubmit()"
    @closed="emit('closed')"
  >
    <el-radio-group v-model="language" class="mb-4" :aria-label="t('common.language')">
      <el-radio-button v-for="code in LANGUAGES" :key="code" :value="code">
        {{ t(`admin.languages.${code}`) }}
      </el-radio-button>
    </el-radio-group>
    <div v-for="code in LANGUAGES" v-show="code === language" :key="code">
      <AppField :name="`names.${code}`" :label="t('admin.common.name')">
        <el-input
          v-model="form.values.names[code]"
          maxlength="300"
          :data-testid="`attribute-name-${code}`"
        />
      </AppField>
    </div>
    <AppField
      name="code"
      :label="t('admin.attributes.code')"
      :hint="t('admin.attributes.codeHint')"
    >
      <el-input v-model="form.values.code" maxlength="200" data-testid="attribute-code" />
    </AppField>
    <AppField name="dataType" :label="t('admin.attributes.dataType')">
      <el-select v-model="form.values.dataType" data-testid="attribute-type">
        <el-option
          v-for="type in DATA_TYPES"
          :key="type"
          :value="type"
          :label="t(`admin.attributes.type_${type}`)"
        />
      </el-select>
    </AppField>
    <AppField
      v-if="form.values.dataType === 'select'"
      name="options"
      :label="t('admin.attributes.options')"
      :hint="t('admin.attributes.optionsHint')"
    >
      <el-select
        v-model="form.values.options"
        multiple
        filterable
        allow-create
        default-first-option
        :reserve-keyword="false"
        data-testid="attribute-options"
      />
    </AppField>
  </FormDrawer>
</template>
