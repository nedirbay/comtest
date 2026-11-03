<script setup lang="ts">
import { ApiError, unwrap, type Schema } from '@comtest/api-client'
import { useApi, useAuthStore } from '@comtest/auth'
import { AppButton, AppField, useApiErrorMessage } from '@comtest/ui'
import { useQueryClient } from '@tanstack/vue-query'
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { z } from '@comtest/ui/zod'
import { Permission } from '@/app/permissions'
import FormDrawer from '@/shared/components/FormDrawer.vue'
import { useAdminForm } from '@/shared/composables/useAdminForm'
import { LANGUAGES, localizedName } from '@/shared/localized'
import { toastError, toastSuccess } from '@/shared/toast'
import { nullIfEmpty, optionalText, slugField, slugify } from '@/shared/validation'
import { subtreeIds } from '../categoryTree'
import { CATEGORIES_KEY, useAttributes } from '../queries'

const props = defineProps<{
  category: Schema<'CategoryDto'> | null
  parentId: string | null
  categories: readonly Schema<'CategoryDto'>[]
}>()
const emit = defineEmits<{ closed: [] }>()
const open = defineModel<boolean>({ required: true })

const { t, locale } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const queryClient = useQueryClient()
const describeError = useApiErrorMessage()
const attributes = useAttributes()

const canWrite = computed(() => auth.can(Permission.CatalogWrite))

const translation = z.object({ name: optionalText(300), description: optionalText(5000) })

const schema = z
  .object({
    parentId: z.string(),
    slug: slugField,
    sortOrder: z.number().int().min(0).max(100_000),
    isActive: z.boolean(),
    translations: z.object({ tk: translation, ru: translation, en: translation }),
  })
  .superRefine((values, context) => {
    if (LANGUAGES.every((language) => !values.translations[language].name)) {
      context.addIssue({
        code: 'custom',
        path: ['translations', 'tk', 'name'],
        message: 'admin.productForm.nameNeeded',
      })
    }
  })

function translationOf(language: string) {
  const found = props.category?.translations.find((item) => item.language === language)

  return { name: found?.name ?? '', description: found?.description ?? '' }
}

const form = useAdminForm({
  schema,
  initial: {
    parentId: props.category?.parentId ?? props.parentId ?? '',
    slug: props.category?.slug ?? '',
    sortOrder: props.category?.sortOrder ?? 0,
    isActive: props.category?.isActive ?? true,
    translations: { tk: translationOf('tk'), ru: translationOf('ru'), en: translationOf('en') },
  },
  submit: async (values) => {
    const body: Schema<'SaveCategoryRequest'> = {
      parentId: values.parentId || null,
      slug: values.slug,
      sortOrder: values.sortOrder,
      isActive: values.isActive,
      version: props.category?.version ?? null,
      translations: LANGUAGES.filter((language) => values.translations[language].name).map(
        (language) => ({
          language,
          name: values.translations[language].name,
          description: nullIfEmpty(values.translations[language].description),
        }),
      ),
    }

    await unwrap(
      props.category
        ? api.client.PUT('/api/v1/admin/catalog/categories/{id}', {
            params: { path: { id: props.category.id } },
            body,
          })
        : api.client.POST('/api/v1/admin/catalog/categories', { body }),
    )

    await queryClient.invalidateQueries({ queryKey: CATEGORIES_KEY })
    toastSuccess(t('admin.categories.saved'))
    open.value = false
  },
})

const slugTouched = ref(Boolean(props.category))

watch(
  () => form.values.translations.tk.name,
  (name) => {
    if (!slugTouched.value) {
      form.values.slug = slugify(name)
    }
  },
)

const parentChoices = computed(() => {
  const excluded = props.category ? subtreeIds(props.categories, props.category.id) : new Set()

  return props.categories.filter((category) => !excluded.has(category.id))
})

const language = ref<(typeof LANGUAGES)[number]>('tk')

// The API can add a link but cannot list or remove one. See the note under the control.
const attributeToLink = ref('')
const attributeRequired = ref(false)
const linking = ref(false)

async function linkAttribute(): Promise<void> {
  const categoryId = props.category?.id

  if (!categoryId || !attributeToLink.value) {
    return
  }

  linking.value = true

  try {
    await unwrap(
      api.client.POST('/api/v1/admin/catalog/categories/{id}/attributes', {
        params: { path: { id: categoryId } },
        body: { attributeId: attributeToLink.value, isRequired: attributeRequired.value },
      }),
    )
    toastSuccess(t('admin.categories.attributeLinked'))
    attributeToLink.value = ''
    attributeRequired.value = false
  } catch (error) {
    toastError(error instanceof ApiError ? describeError(error) : t('errors.unexpected'))
  } finally {
    linking.value = false
  }
}
</script>

<template>
  <FormDrawer
    v-model="open"
    :title="t(category ? 'admin.categories.edit' : 'admin.categories.new')"
    :error="form.formError.value"
    :submitting="form.submitting.value"
    :readonly="!canWrite"
    @submit="form.handleSubmit()"
    @closed="emit('closed')"
  >
    <el-radio-group v-model="language" class="mb-4" :aria-label="t('common.language')">
      <el-radio-button v-for="code in LANGUAGES" :key="code" :value="code">
        {{ t(`admin.languages.${code}`) }}
      </el-radio-button>
    </el-radio-group>
    <div v-for="code in LANGUAGES" v-show="code === language" :key="code">
      <AppField :name="`translations.${code}.name`" :label="t('admin.common.name')">
        <el-input
          v-model="form.values.translations[code].name"
          maxlength="300"
          :data-testid="`category-name-${code}`"
        />
      </AppField>
      <AppField :name="`translations.${code}.description`" :label="t('admin.common.description')">
        <el-input
          v-model="form.values.translations[code].description"
          type="textarea"
          :rows="3"
          maxlength="5000"
        />
      </AppField>
    </div>
    <AppField name="slug" :label="t('admin.products.slug')" :hint="t('admin.productForm.slugHint')">
      <el-input
        v-model="form.values.slug"
        maxlength="200"
        data-testid="category-slug"
        @input="slugTouched = true"
      />
    </AppField>
    <AppField name="parentId" :label="t('admin.categories.parent')">
      <el-select
        v-model="form.values.parentId"
        clearable
        filterable
        value-on-clear=""
        :empty-values="['', null, undefined]"
        :placeholder="t('admin.categories.noParent')"
        data-testid="category-parent"
      >
        <el-option
          v-for="item in parentChoices"
          :key="item.id"
          :value="item.id"
          :label="localizedName(item.translations, locale)"
        />
      </el-select>
    </AppField>
    <AppField name="sortOrder" :label="t('admin.categories.sortOrder')">
      <el-input-number v-model="form.values.sortOrder" :min="0" :max="100000" :step="10" />
    </AppField>
    <AppField name="isActive">
      <el-switch v-model="form.values.isActive" :active-text="t('admin.common.active')" />
    </AppField>

    <section v-if="category && canWrite" class="mt-4 rounded-card border border-line p-3">
      <h3 class="font-semibold text-ink">{{ t('admin.categories.linkAttribute') }}</h3>
      <p class="mb-2 text-xs text-ink-subtle">{{ t('admin.categories.linkHint') }}</p>
      <div class="flex flex-wrap items-center gap-3">
        <el-select
          v-model="attributeToLink"
          class="!w-full sm:!w-56"
          filterable
          :placeholder="t('admin.categories.chooseAttribute')"
          :aria-label="t('admin.categories.chooseAttribute')"
          data-testid="category-attribute"
        >
          <el-option
            v-for="attribute in attributes.data.value"
            :key="attribute.id"
            :value="attribute.id"
            :label="localizedName(attribute.translations, locale) || attribute.code"
          />
        </el-select>
        <el-checkbox v-model="attributeRequired" :label="t('admin.categories.required')" />
        <AppButton
          :loading="linking"
          :disabled="!attributeToLink"
          data-testid="category-attribute-link"
          @click="linkAttribute"
        >
          {{ t('admin.categories.link') }}
        </AppButton>
      </div>
    </section>
  </FormDrawer>
</template>
