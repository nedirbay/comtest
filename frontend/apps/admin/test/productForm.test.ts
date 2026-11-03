import { describe, expect, it } from 'vitest'
import {
  emptyProduct,
  mapServerField,
  productSchema,
  tabOfField,
  toFormValues,
  toProductRequest,
} from '../src/features/catalog/productForm'

function validProduct() {
  const values = emptyProduct('TMT')

  values.slug = 'pagta-koynek'
  values.translations.tk.name = 'Pagta köýnek'
  values.variants[0]!.sku = 'A-1'
  values.variants[0]!.price = '149.90'

  return values
}

function issues(values: unknown): string[] {
  const parsed = productSchema.safeParse(values)

  return parsed.success ? [] : parsed.error.issues.map((issue) => issue.path.join('.'))
}

describe('the product form', () => {
  it('accepts the smallest complete product', () => {
    expect(issues(validProduct())).toEqual([])
  })

  it('needs a name in at least one language', () => {
    const values = validProduct()

    values.translations.tk.name = ''

    expect(issues(values)).toContain('translations.tk.name')
  })

  it('needs a name in a language that has other text', () => {
    const values = validProduct()

    values.translations.ru.description = '<p>Текст</p>'

    expect(issues(values)).toEqual(['translations.ru.name'])
  })

  it('needs a variant, a price in a sane form, and SKUs that differ', () => {
    const none = validProduct()

    none.variants = []
    expect(issues(none)).toEqual(['variants'])

    const badPrice = validProduct()

    badPrice.variants[0]!.price = '12,5'
    expect(issues(badPrice)).toEqual(['variants.0.price'])

    const twins = validProduct()

    twins.variants.push({ ...twins.variants[0]! })
    expect(issues(twins)).toEqual(['variants.1.sku'])
  })

  it('refuses an address the API would refuse', () => {
    const values = validProduct()

    values.slug = 'Bad Slug'
    expect(issues(values)).toEqual(['slug'])
  })

  it('sends only the translated languages, and empty text as nothing', () => {
    const values = validProduct()

    values.variants[0]!.options = [
      { key: 'Reňk', value: 'Gök' },
      { key: '', value: 'ýarym' },
    ]

    const request = toProductRequest(productSchema.parse(values), 7)

    expect(request.translations).toHaveLength(1)
    expect(request.translations[0]).toMatchObject({ language: 'tk', description: null })
    expect(request.version).toBe(7)
    expect(request.categoryId).toBeNull()
    expect(request.variants[0]).toMatchObject({
      price: 149.9,
      compareAtAmount: null,
      costAmount: null,
      options: { Reňk: 'Gök' },
    })
  })

  it('leaves out attributes the user did not fill', () => {
    const values = validProduct()

    values.attributes = { one: 'Pagta', two: '' }

    expect(toProductRequest(productSchema.parse(values), null).attributes).toEqual([
      { attributeId: 'one', value: 'Pagta' },
    ])
  })

  it('reads a product from the API back into the same request', () => {
    const product = {
      id: 'p1',
      slug: 'bir',
      status: 'Draft',
      categoryId: 'c1',
      brandId: null,
      taxGroup: null,
      translations: [
        {
          language: 'ru',
          name: 'Один',
          shortDescription: null,
          description: '<p>Текст</p>',
          metaTitle: 'М',
          metaDescription: null,
        },
      ],
      variants: [
        {
          id: 'v1',
          sku: 'S',
          barcode: null,
          price: 10.5,
          currency: 'USD',
          compareAtAmount: 12,
          costAmount: null,
          options: { size: 'M' },
          isActive: false,
        },
      ],
      attributes: [{ attributeId: 'a1', value: 'x' }],
      images: [{ mediaAssetId: 'm1', altText: null, thumbnail: 't', medium: 'm', large: 'l' }],
      version: 3,
    }

    const request = toProductRequest(productSchema.parse(toFormValues(product)), product.version)

    expect(request).toMatchObject({
      slug: 'bir',
      categoryId: 'c1',
      version: 3,
      translations: [{ language: 'ru', name: 'Один', description: '<p>Текст</p>', metaTitle: 'М' }],
      variants: [{ id: 'v1', price: 10.5, currency: 'USD', compareAtAmount: 12, isActive: false }],
      attributes: [{ attributeId: 'a1', value: 'x' }],
      images: [{ mediaAssetId: 'm1', altText: null }],
    })
  })

  it('points a server error at the input it is about', () => {
    const request = toProductRequest(productSchema.parse(validProduct()), null)

    expect(mapServerField('translations[0].name', request)).toBe('translations.tk.name')
    expect(mapServerField('variants[2].sku', request)).toBe('variants.2.sku')
    expect(mapServerField('slug', request)).toBe('slug')
  })

  it('knows which tab holds a field', () => {
    expect(tabOfField('slug')).toBe('main')
    expect(tabOfField('translations.ru.name')).toBe('translations')
    expect(tabOfField('translations.ru.metaTitle')).toBe('seo')
    expect(tabOfField('variants.1.price')).toBe('variants')
    expect(tabOfField('images')).toBe('images')
    expect(tabOfField('attributes.x')).toBe('attributes')
  })
})
