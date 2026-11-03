import type { Schema } from '@comtest/api-client'
import { z } from '@comtest/ui/zod'
import { LANGUAGES } from '@/shared/localized'
import {
  nullIfEmpty,
  optionalAmount,
  optionalText,
  requiredAmount,
  requiredText,
  slugField,
  toAmount,
} from '@/shared/validation'

const translation = z.object({
  name: optionalText(300),
  shortDescription: optionalText(1000),
  description: z.string().max(100_000, 'validation.tooLong'),
  metaTitle: optionalText(200),
  metaDescription: optionalText(500),
})

export type TranslationValues = z.input<typeof translation>

const optionRow = z.object({
  key: optionalText(100),
  value: optionalText(200),
})

const variant = z.object({
  id: z.string().nullable(),
  sku: requiredText(100),
  barcode: optionalText(50),
  price: requiredAmount,
  currency: z.string().min(1, 'validation.required'),
  compareAt: optionalAmount,
  cost: optionalAmount,
  isActive: z.boolean(),
  options: z.array(optionRow),
})

export type VariantValues = z.input<typeof variant>

const image = z.object({
  mediaAssetId: z.string(),
  altText: optionalText(300),
  thumbnail: z.string().nullable(),
  medium: z.string().nullable(),
})

export type ImageValues = z.input<typeof image>

export const productSchema = z
  .object({
    slug: slugField,
    categoryId: z.string(),
    brandId: z.string(),
    taxGroup: optionalText(50),
    translations: z.object({ tk: translation, ru: translation, en: translation }),
    variants: z.array(variant),
    attributes: z.record(z.string(), z.string()),
    images: z.array(image).max(30, 'admin.productForm.tooManyImages'),
  })
  .superRefine((values, context) => {
    const named = LANGUAGES.filter((language) => values.translations[language].name.trim())

    if (named.length === 0) {
      context.addIssue({
        code: 'custom',
        path: ['translations', LANGUAGES[0] as string, 'name'],
        message: 'admin.productForm.nameNeeded',
      })
    }

    for (const language of LANGUAGES) {
      const { name, ...rest } = values.translations[language]

      if (!name.trim() && Object.values(rest).some((text) => text.trim())) {
        context.addIssue({
          code: 'custom',
          path: ['translations', language, 'name'],
          message: 'admin.productForm.nameForText',
        })
      }
    }

    if (values.variants.length === 0) {
      context.addIssue({
        code: 'custom',
        path: ['variants'],
        message: 'admin.productForm.variantNeeded',
      })
    }

    const seen = new Set<string>()

    values.variants.forEach((item, index) => {
      if (seen.has(item.sku)) {
        context.addIssue({
          code: 'custom',
          path: ['variants', index, 'sku'],
          message: 'admin.productForm.skuRepeated',
        })
      }

      seen.add(item.sku)
    })
  })

export type ProductValues = z.input<typeof productSchema>

function emptyTranslation(): TranslationValues {
  return { name: '', shortDescription: '', description: '', metaTitle: '', metaDescription: '' }
}

export function emptyVariant(currency: string): VariantValues {
  return {
    id: null,
    sku: '',
    barcode: '',
    price: '',
    currency,
    compareAt: '',
    cost: '',
    isActive: true,
    options: [],
  }
}

export function emptyProduct(currency: string): ProductValues {
  return {
    slug: '',
    categoryId: '',
    brandId: '',
    taxGroup: '',
    translations: { tk: emptyTranslation(), ru: emptyTranslation(), en: emptyTranslation() },
    variants: [emptyVariant(currency)],
    attributes: {},
    images: [],
  }
}

const amountText = (value: number | null) => (value === null ? '' : String(value))

export function toFormValues(product: Schema<'ProductAdminDto'>): ProductValues {
  const values = emptyProduct('')

  values.slug = product.slug
  values.categoryId = product.categoryId ?? ''
  values.brandId = product.brandId ?? ''
  values.taxGroup = product.taxGroup ?? ''

  for (const item of product.translations) {
    if (item.language === 'tk' || item.language === 'ru' || item.language === 'en') {
      values.translations[item.language] = {
        name: item.name,
        shortDescription: item.shortDescription ?? '',
        description: item.description ?? '',
        metaTitle: item.metaTitle ?? '',
        metaDescription: item.metaDescription ?? '',
      }
    }
  }

  values.variants = product.variants.map((item) => ({
    id: item.id,
    sku: item.sku,
    barcode: item.barcode ?? '',
    price: String(item.price),
    currency: item.currency,
    compareAt: amountText(item.compareAtAmount),
    cost: amountText(item.costAmount),
    isActive: item.isActive,
    options: Object.entries(item.options).map(([key, value]) => ({ key, value })),
  }))
  values.attributes = Object.fromEntries(
    product.attributes.map((item) => [item.attributeId, item.value]),
  )
  values.images = product.images.map((item) => ({
    mediaAssetId: item.mediaAssetId,
    altText: item.altText ?? '',
    thumbnail: item.thumbnail,
    medium: item.medium,
  }))

  return values
}

/** Only languages with a name go to the API, as the others are not translated yet. */
export function toProductRequest(
  values: z.output<typeof productSchema>,
  version: number | null,
): Schema<'SaveProductRequest'> {
  return {
    slug: values.slug,
    categoryId: values.categoryId || null,
    brandId: values.brandId || null,
    taxGroup: nullIfEmpty(values.taxGroup),
    translations: LANGUAGES.filter((language) => values.translations[language].name).map(
      (language) => {
        const item = values.translations[language]

        return {
          language,
          name: item.name,
          shortDescription: nullIfEmpty(item.shortDescription),
          description: nullIfEmpty(item.description),
          metaTitle: nullIfEmpty(item.metaTitle),
          metaDescription: nullIfEmpty(item.metaDescription),
        }
      },
    ),
    variants: values.variants.map((item) => ({
      id: item.id,
      sku: item.sku,
      barcode: nullIfEmpty(item.barcode),
      price: Number(item.price),
      currency: item.currency,
      compareAtAmount: toAmount(item.compareAt),
      costAmount: toAmount(item.cost),
      isActive: item.isActive,
      options: Object.fromEntries(
        item.options.filter((row) => row.key && row.value).map((row) => [row.key, row.value]),
      ),
    })),
    attributes: Object.entries(values.attributes)
      .filter(([, value]) => value !== '')
      .map(([attributeId, value]) => ({ attributeId, value })),
    images: values.images.map((item) => ({
      mediaAssetId: item.mediaAssetId,
      altText: nullIfEmpty(item.altText),
    })),
    version,
  }
}

export type ProductTab = 'main' | 'translations' | 'variants' | 'images' | 'attributes' | 'seo'

/** Which tab holds a field, to take the user to the first error. */
export function tabOfField(field: string): ProductTab {
  if (field.startsWith('translations.')) {
    return /\.(metaTitle|metaDescription)$/.test(field) ? 'seo' : 'translations'
  }

  if (field.startsWith('variants')) {
    return 'variants'
  }

  if (field.startsWith('images')) {
    return 'images'
  }

  return field.startsWith('attributes') ? 'attributes' : 'main'
}

/**
 * The server numbers the translations it was sent, and the form keys them by language. This points
 * the server's `translations[1].name` at the input it means, and `variants[0].sku` likewise.
 */
export function mapServerField(field: string, request: Schema<'SaveProductRequest'>): string {
  const translated = /^translations\[(\d+)\]\.(.+)$/.exec(field)

  if (translated) {
    const language = request.translations[Number(translated[1])]?.language

    return language ? `translations.${language}.${translated[2]}` : field
  }

  return field.replace(/\[(\d+)\]/g, '.$1')
}
