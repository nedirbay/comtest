<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { AppField } from '@comtest/ui'
import { useQueryClient } from '@tanstack/vue-query'
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { z } from '@comtest/ui/zod'
import FormDrawer from '@/shared/components/FormDrawer.vue'
import { useAdminForm } from '@/shared/composables/useAdminForm'
import { toastSuccess } from '@/shared/toast'
import { nullIfEmpty, optionalUrl, requiredText, slugField, slugify } from '@/shared/validation'
import { BRANDS_KEY } from '../queries'

const props = defineProps<{ brand: Schema<'BrandDto'> | null; readonly?: boolean }>()
const emit = defineEmits<{ closed: [] }>()
const open = defineModel<boolean>({ required: true })

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const queryClient = useQueryClient()

const form = useAdminForm({
  schema: z.object({ name: requiredText(300), slug: slugField, logoUrl: optionalUrl }),
  initial: {
    name: props.brand?.name ?? '',
    slug: props.brand?.slug ?? '',
    logoUrl: props.brand?.logoUrl ?? '',
  },
  submit: async (values) => {
    const body: Schema<'SaveBrandRequest'> = {
      name: values.name,
      slug: values.slug,
      logoUrl: nullIfEmpty(values.logoUrl),
      version: props.brand?.version ?? null,
    }

    await unwrap(
      props.brand
        ? api.client.PUT('/api/v1/admin/catalog/brands/{id}', {
            params: { path: { id: props.brand.id } },
            body,
          })
        : api.client.POST('/api/v1/admin/catalog/brands', { body }),
    )

    await queryClient.invalidateQueries({ queryKey: BRANDS_KEY })
    toastSuccess(t('admin.brands.saved'))
    open.value = false
  },
})

const slugTouched = ref(Boolean(props.brand))

watch(
  () => form.values.name,
  (name) => {
    if (!slugTouched.value) {
      form.values.slug = slugify(name)
    }
  },
)
</script>

<template>
  <FormDrawer
    v-model="open"
    :title="t(brand ? 'admin.brands.edit' : 'admin.brands.new')"
    :error="form.formError.value"
    :submitting="form.submitting.value"
    :readonly="readonly"
    @submit="form.handleSubmit()"
    @closed="emit('closed')"
  >
    <AppField name="name" :label="t('admin.common.name')">
      <el-input v-model="form.values.name" maxlength="300" data-testid="brand-name" />
    </AppField>
    <AppField name="slug" :label="t('admin.products.slug')" :hint="t('admin.productForm.slugHint')">
      <el-input
        v-model="form.values.slug"
        maxlength="200"
        data-testid="brand-slug"
        @input="slugTouched = true"
      />
    </AppField>
    <AppField name="logoUrl" :label="t('admin.brands.logoUrl')" :hint="t('admin.brands.logoHint')">
      <el-input v-model="form.values.logoUrl" maxlength="500" inputmode="url" />
    </AppField>
  </FormDrawer>
</template>
