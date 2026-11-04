<script setup lang="ts">
import { ApiError, ErrorCode, unwrap, type Schema } from '@comtest/api-client'
import { useApi, useAuthStore } from '@comtest/auth'
import {
  AppButton,
  AppField,
  AppForm,
  ErrorState,
  PageHeader,
  SkeletonBlock,
  StatusTag,
} from '@comtest/ui'
import { useQuery, useQueryClient } from '@tanstack/vue-query'
import { computed, defineAsyncComponent, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { Permission } from '@/app/permissions'
import { usePageTitle } from '@/features/shell/useShellStore'
import { useAdminForm } from '@/shared/composables/useAdminForm'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useUnsavedChanges } from '@/shared/composables/useUnsavedChanges'
import { LANGUAGES, localizedName } from '@/shared/localized'
import { toastSuccess } from '@/shared/toast'
import { slugify } from '@/shared/validation'
import PriceHistory from '../components/PriceHistory.vue'
import ProductImages from '../components/ProductImages.vue'
import VariantsEditor from '../components/VariantsEditor.vue'
import {
  emptyProduct,
  mapServerField,
  productSchema,
  tabOfField,
  toFormValues,
  toProductRequest,
  type ProductTab,
} from '../productForm'
import { PRODUCTS_KEY, useAttributes, useBrands, useCategories, useStoreInfo } from '../queries'

// The editor is the heaviest part of the page, and read-only staff may never open its tab.
const RichTextEditor = defineAsyncComponent(() => import('../components/RichTextEditor.vue'))

const { t, locale } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const route = useRoute()
const router = useRouter()
const queryClient = useQueryClient()
const confirm = useConfirm()

const productId = computed(() => (route.name === 'product-edit' ? String(route.params.id) : null))
const canWrite = computed(() => auth.can(Permission.CatalogWrite))
const readOnly = computed(() => !canWrite.value)

const store = useStoreInfo()
const categories = useCategories()
const brands = useBrands()
const attributes = useAttributes()

const product = useQuery({
  queryKey: [...PRODUCTS_KEY, 'detail', productId],
  enabled: computed(() => productId.value !== null),
  queryFn: () =>
    unwrap(
      api.client.GET('/api/v1/admin/catalog/products/{id}', {
        params: { path: { id: productId.value as string } },
      }),
    ),
})

const tab = ref<ProductTab | 'history'>('main')
const language = ref<(typeof LANGUAGES)[number]>('tk')
const seoLanguage = ref<(typeof LANGUAGES)[number]>('tk')
const version = ref<number | null>(null)
const baseline = ref('')
const imagesBusy = ref(false)
const slugTouched = ref(false)
const slugLanguage = ref<(typeof LANGUAGES)[number] | null>(null)

const form = useAdminForm({
  schema: productSchema,
  initial: emptyProduct('TMT'),
  submit: async (values) => {
    const request = toProductRequest(values, version.value)

    try {
      const saved = productId.value
        ? await unwrap(
            api.client.PUT('/api/v1/admin/catalog/products/{id}', {
              params: { path: { id: productId.value } },
              body: request,
            }),
          )
        : await unwrap(api.client.POST('/api/v1/admin/catalog/products', { body: request }))

      queryClient.setQueryData([...PRODUCTS_KEY, 'detail', saved.id], saved)
      await queryClient.invalidateQueries({ queryKey: [...PRODUCTS_KEY, 'list'] })
      applyProduct(saved)
      toastSuccess(t('admin.productForm.saved'))

      if (!productId.value) {
        await router.replace({ name: 'product-edit', params: { id: saved.id } })
      }
    } catch (error) {
      if (error instanceof ApiError && Object.keys(error.fieldErrors).length > 0) {
        throw new ApiError({
          status: error.status,
          code: error.code,
          title: error.message,
          correlationId: error.correlationId,
          fieldErrors: Object.fromEntries(
            Object.entries(error.fieldErrors).map(([field, messages]) => [
              mapServerField(field, request),
              [...messages],
            ]),
          ),
        })
      }

      throw error
    }
  },
})

function snapshot(): string {
  return JSON.stringify(form.values)
}

function applyProduct(next: Schema<'ProductAdminDto'>): void {
  Object.assign(form.values, toFormValues(next))
  version.value = next.version
  baseline.value = snapshot()
}

const isDirty = computed(() => baseline.value !== '' && snapshot() !== baseline.value)

useUnsavedChanges(isDirty)

// Applied once. A later refetch must not overwrite what the user has typed since.
watch(
  () => product.data.value,
  (next) => {
    if (next && version.value === null) {
      applyProduct(next)
    }
  },
  { immediate: true },
)

// A new product starts in the store's own currency and language.
watch(
  () => store.data.value,
  (info) => {
    if (!info) {
      return
    }

    if (!productId.value && !isDirty.value) {
      for (const variant of form.values.variants) {
        variant.currency = info.defaultCurrency
      }

      baseline.value = snapshot()
    }

    // Not once the user has started: the tab they are typing in must not change under them.
    if (!isDirty.value && LANGUAGES.includes(info.defaultLanguage as (typeof LANGUAGES)[number])) {
      language.value = info.defaultLanguage as (typeof LANGUAGES)[number]
      seoLanguage.value = language.value
    }
  },
  { immediate: true },
)

if (!productId.value) {
  baseline.value = snapshot()
}

// Fills the address from the first name written, until the user writes their own. Filling in a
// translation afterwards must not change it.
watch(
  () => LANGUAGES.map((code) => form.values.translations[code].name),
  (names, previous) => {
    if (productId.value || slugTouched.value) {
      return
    }

    const index = LANGUAGES.findIndex((code) => code === slugLanguage.value)
    const changed = names.findIndex((name, position) => name !== previous[position])

    if (changed === -1) {
      return
    }

    if (index === -1 || !names[index]) {
      slugLanguage.value = LANGUAGES[changed] as (typeof LANGUAGES)[number]
    }

    const source = LANGUAGES.findIndex((code) => code === slugLanguage.value)

    form.values.slug = slugify(names[source] ?? '')
  },
)

const isConflict = computed(
  () =>
    form.lastError.value instanceof ApiError &&
    form.lastError.value.code === ErrorCode.ConcurrencyConflict,
)

const errorList = computed(() => Object.values(form.errors.value))

// The first thing wrong may sit on a tab the user cannot see.
watch(form.errors, (errors) => {
  const first = Object.keys(errors)[0]

  if (!first) {
    return
  }

  tab.value = tabOfField(first)

  const match = /^translations\.(tk|ru|en)\./.exec(first)

  if (match) {
    const named = match[1] as (typeof LANGUAGES)[number]

    language.value = named
    seoLanguage.value = named
  }
})

async function reload(): Promise<void> {
  const fresh = await product.refetch()

  if (fresh.data) {
    form.lastError.value = null
    form.formError.value = null
    applyProduct(fresh.data)
  }
}

const status = computed(() => product.data.value?.status)

function setStatus(next: Schema<'ProductStatus'>): void {
  const id = productId.value

  if (!id) {
    return
  }

  const action = async () => {
    const saved = await unwrap(
      api.client.PUT('/api/v1/admin/catalog/products/{id}/status', {
        params: { path: { id } },
        body: { status: next },
      }),
    )

    queryClient.setQueryData([...PRODUCTS_KEY, 'detail', id], saved)
    await queryClient.invalidateQueries({ queryKey: [...PRODUCTS_KEY, 'list'] })
    version.value = saved.version
    toastSuccess(t('admin.productForm.statusChanged'))
  }

  confirm.ask({
    title: t(`admin.productForm.status_${next}`),
    message: t(`admin.productForm.status_${next}_confirm`),
    action,
  })
}

function deleteProduct(): void {
  const id = productId.value

  if (!id) {
    return
  }

  confirm.ask({
    title: t('admin.products.deleteOne'),
    message: t('admin.products.deleteOneConfirm'),
    confirmText: t('common.delete'),
    danger: true,
    action: async () => {
      await unwrap(
        api.client.DELETE('/api/v1/admin/catalog/products/{id}', { params: { path: { id } } }),
      )
      await queryClient.invalidateQueries({ queryKey: [...PRODUCTS_KEY, 'list'] })
      baseline.value = snapshot()
      await router.push({ name: 'products' })
    },
  })
}

const categoryName = (category: Schema<'CategoryDto'>) =>
  localizedName(category.translations, locale.value)

const title = computed(() => {
  const names = form.values.translations

  return productId.value
    ? names[locale.value as (typeof LANGUAGES)[number]]?.name ||
        names.tk.name ||
        names.ru.name ||
        names.en.name
    : null
})

usePageTitle(title)

function attributeLabel(attribute: Schema<'AttributeDto'>): string {
  return localizedName(attribute.translations, locale.value) || attribute.code
}

function attributeValue(id: string): string {
  return form.values.attributes[id] ?? ''
}

function setAttribute(id: string, value: unknown): void {
  form.values.attributes[id] = value === null || value === undefined ? '' : String(value)
}
</script>

<template>
  <ErrorState
    v-if="product.isError.value"
    :error="product.error.value"
    retryable
    @retry="product.refetch()"
  />
  <SkeletonBlock v-else-if="productId && product.isPending.value" :lines="8" />
  <template v-else>
    <PageHeader
      :title="title || t(productId ? 'admin.products.edit' : 'admin.products.new')"
      :description="readOnly ? t('admin.productForm.readOnly') : undefined"
    >
      <template #actions>
        <StatusTag v-if="productId && status" kind="productStatus" :value="status" />
        <template v-if="productId && canWrite">
          <AppButton
            v-if="status !== 'published'"
            data-testid="product-publish"
            @click="setStatus('published')"
          >
            {{ t('admin.productForm.publish') }}
          </AppButton>
          <AppButton
            v-if="status === 'published'"
            data-testid="product-draft"
            @click="setStatus('draft')"
          >
            {{ t('admin.productForm.toDraft') }}
          </AppButton>
          <AppButton
            v-if="status !== 'archived'"
            data-testid="product-archive"
            @click="setStatus('archived')"
          >
            {{ t('admin.productForm.archive') }}
          </AppButton>
          <AppButton variant="danger" data-testid="product-delete" @click="deleteProduct">
            {{ t('common.delete') }}
          </AppButton>
        </template>
        <AppButton
          v-if="canWrite"
          variant="primary"
          :loading="form.submitting.value"
          :disabled="imagesBusy"
          data-testid="product-save"
          @click="form.handleSubmit()"
        >
          {{ t('common.save') }}
        </AppButton>
      </template>
    </PageHeader>

    <el-alert
      v-if="isConflict"
      type="warning"
      show-icon
      :closable="false"
      class="mb-4"
      role="alert"
      :title="t('admin.productForm.conflictTitle')"
      data-testid="conflict"
    >
      <p class="mb-2">{{ form.formError.value }}</p>
      <p class="mb-2">{{ t('admin.productForm.conflictText') }}</p>
      <AppButton data-testid="conflict-reload" @click="reload">
        {{ t('admin.productForm.conflictReload') }}
      </AppButton>
    </el-alert>

    <el-alert
      v-if="errorList.length"
      type="error"
      show-icon
      :closable="false"
      class="mb-4"
      role="alert"
      :title="t('admin.productForm.fixErrors', { count: errorList.length })"
      data-testid="error-summary"
    />

    <p v-if="imagesBusy" class="mb-3 text-sm text-ink-muted">
      {{ t('admin.productForm.waitForImages') }}
    </p>

    <AppForm
      :error="isConflict ? null : form.formError.value"
      :disabled="readOnly"
      @submit="form.handleSubmit()"
    >
      <div class="rounded-card border border-line bg-surface p-3 sm:p-5">
        <el-tabs v-model="tab" data-testid="product-tabs">
          <el-tab-pane :label="t('admin.productForm.tabMain')" name="main">
            <div class="grid gap-x-4 md:grid-cols-2">
              <AppField
                name="slug"
                :label="t('admin.products.slug')"
                :hint="t('admin.productForm.slugHint')"
              >
                <el-input
                  v-model="form.values.slug"
                  maxlength="200"
                  data-testid="product-slug"
                  @input="slugTouched = true"
                />
              </AppField>
              <AppField name="taxGroup" :label="t('admin.productForm.taxGroup')">
                <el-input v-model="form.values.taxGroup" maxlength="50" />
              </AppField>
              <AppField name="categoryId" :label="t('admin.productForm.category')">
                <el-select
                  v-model="form.values.categoryId"
                  clearable
                  filterable
                  value-on-clear=""
                  :empty-values="['', null, undefined]"
                  :placeholder="t('admin.productForm.noCategory')"
                  data-testid="product-category"
                >
                  <el-option
                    v-for="category in categories.data.value"
                    :key="category.id"
                    :value="category.id"
                    :label="categoryName(category)"
                  />
                </el-select>
              </AppField>
              <AppField name="brandId" :label="t('admin.productForm.brand')">
                <el-select
                  v-model="form.values.brandId"
                  clearable
                  filterable
                  value-on-clear=""
                  :empty-values="['', null, undefined]"
                  :placeholder="t('admin.productForm.noBrand')"
                  data-testid="product-brand"
                >
                  <el-option
                    v-for="brand in brands.data.value"
                    :key="brand.id"
                    :value="brand.id"
                    :label="brand.name"
                  />
                </el-select>
              </AppField>
            </div>
          </el-tab-pane>

          <el-tab-pane :label="t('admin.productForm.tabTranslations')" name="translations" lazy>
            <el-radio-group
              v-model="language"
              class="mb-4"
              :aria-label="t('common.language')"
              data-testid="language-tabs"
            >
              <el-radio-button v-for="code in LANGUAGES" :key="code" :value="code">
                {{ t(`admin.languages.${code}`) }}
              </el-radio-button>
            </el-radio-group>
            <div v-for="code in LANGUAGES" v-show="code === language" :key="code">
              <AppField :name="`translations.${code}.name`" :label="t('admin.products.name')">
                <el-input
                  v-model="form.values.translations[code].name"
                  maxlength="300"
                  :data-testid="`name-${code}`"
                />
              </AppField>
              <AppField
                :name="`translations.${code}.shortDescription`"
                :label="t('admin.productForm.shortDescription')"
              >
                <el-input
                  v-model="form.values.translations[code].shortDescription"
                  type="textarea"
                  :rows="2"
                  maxlength="1000"
                  show-word-limit
                  :data-testid="`short-${code}`"
                />
              </AppField>
              <AppField
                :name="`translations.${code}.description`"
                :label="t('admin.productForm.description')"
              >
                <RichTextEditor
                  v-model="form.values.translations[code].description"
                  :disabled="readOnly"
                  :label="t('admin.productForm.description')"
                  :data-testid="`editor-wrap-${code}`"
                />
              </AppField>
            </div>
          </el-tab-pane>

          <el-tab-pane :label="t('admin.productForm.tabVariants')" name="variants">
            <AppField name="variants">
              <VariantsEditor
                v-model="form.values.variants"
                :currency="store.data.value?.defaultCurrency ?? 'TMT'"
                :disabled="readOnly"
              />
            </AppField>
          </el-tab-pane>

          <el-tab-pane :label="t('admin.productForm.tabImages')" name="images">
            <AppField name="images">
              <ProductImages
                v-model="form.values.images"
                v-model:busy="imagesBusy"
                class="w-full"
                :disabled="readOnly"
              />
            </AppField>
          </el-tab-pane>

          <el-tab-pane :label="t('admin.productForm.tabAttributes')" name="attributes" lazy>
            <p class="mb-3 text-sm text-ink-muted">{{ t('admin.productForm.attributesHint') }}</p>
            <p v-if="!attributes.data.value?.length" class="text-sm text-ink-muted">
              {{ t('admin.productForm.noAttributes') }}
            </p>
            <div class="grid gap-x-4 md:grid-cols-2">
              <AppField
                v-for="attribute in attributes.data.value"
                :key="attribute.id"
                :name="`attributes.${attribute.id}`"
                :label="attributeLabel(attribute)"
              >
                <el-select
                  v-if="attribute.dataType === 'select'"
                  :model-value="attributeValue(attribute.id)"
                  clearable
                  value-on-clear=""
                  :empty-values="['', null, undefined]"
                  :data-testid="`attribute-${attribute.code}`"
                  @update:model-value="setAttribute(attribute.id, $event)"
                >
                  <el-option
                    v-for="option in attribute.options"
                    :key="option"
                    :value="option"
                    :label="option"
                  />
                </el-select>
                <el-select
                  v-else-if="attribute.dataType === 'boolean'"
                  :model-value="attributeValue(attribute.id)"
                  clearable
                  value-on-clear=""
                  :empty-values="['', null, undefined]"
                  :data-testid="`attribute-${attribute.code}`"
                  @update:model-value="setAttribute(attribute.id, $event)"
                >
                  <el-option value="true" :label="t('admin.common.yes')" />
                  <el-option value="false" :label="t('admin.common.no')" />
                </el-select>
                <el-input
                  v-else
                  :model-value="attributeValue(attribute.id)"
                  :inputmode="attribute.dataType === 'number' ? 'decimal' : 'text'"
                  :data-testid="`attribute-${attribute.code}`"
                  @update:model-value="setAttribute(attribute.id, $event)"
                />
              </AppField>
            </div>
          </el-tab-pane>

          <el-tab-pane :label="t('admin.productForm.tabSeo')" name="seo" lazy>
            <el-radio-group v-model="seoLanguage" class="mb-4" :aria-label="t('common.language')">
              <el-radio-button v-for="code in LANGUAGES" :key="code" :value="code">
                {{ t(`admin.languages.${code}`) }}
              </el-radio-button>
            </el-radio-group>
            <div v-for="code in LANGUAGES" v-show="code === seoLanguage" :key="code">
              <AppField
                :name="`translations.${code}.metaTitle`"
                :label="t('admin.productForm.metaTitle')"
              >
                <el-input
                  v-model="form.values.translations[code].metaTitle"
                  maxlength="200"
                  show-word-limit
                  :data-testid="`meta-title-${code}`"
                />
              </AppField>
              <AppField
                :name="`translations.${code}.metaDescription`"
                :label="t('admin.productForm.metaDescription')"
              >
                <el-input
                  v-model="form.values.translations[code].metaDescription"
                  type="textarea"
                  :rows="3"
                  maxlength="500"
                  show-word-limit
                />
              </AppField>
            </div>
          </el-tab-pane>

          <el-tab-pane
            v-if="productId"
            :label="t('admin.productForm.tabHistory')"
            name="history"
            lazy
          >
            <PriceHistory :product-id="productId" :variants="product.data.value?.variants ?? []" />
          </el-tab-pane>
        </el-tabs>
      </div>
    </AppForm>
  </template>
</template>
