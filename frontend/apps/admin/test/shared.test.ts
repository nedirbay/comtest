import { describe, expect, it } from 'vitest'
import { visibleSections } from '../src/app/navigation'
import { Permission } from '../src/app/permissions'
import {
  imageFileProblem,
  MAX_IMAGE_BYTES,
} from '../src/features/catalog/components/useImageUpload'
import { nextStatuses } from '../src/features/orders/queries'
import { runBulk } from '../src/shared/bulk'
import { localizedName } from '../src/shared/localized'
import { slugify, toAmount } from '../src/shared/validation'

describe('the side menu', () => {
  const names = (held: string[]) =>
    visibleSections((permission) => held.includes(permission)).flatMap((section) =>
      section.items.map((item) => item.name),
    )

  it('shows staff their items and nothing else', () => {
    const shown = names([Permission.CatalogRead, Permission.OrdersRead])

    expect(shown).toContain('products')
    expect(shown).toContain('orders')
    expect(shown).not.toContain('settings')
    expect(shown).not.toContain('staff')
    expect(shown).not.toContain('tenants')
  })

  it('leaves a platform admin the platform and their own account', () => {
    expect(names([Permission.TenantsManage])).toEqual(['dashboard', 'tenants', 'security'])
  })

  it('drops a section with nothing left in it', () => {
    const sections = visibleSections(() => false)

    expect(sections.map((section) => section.labelKey)).toEqual([
      'admin.nav.sectionMain',
      'admin.nav.sectionAccount',
    ])
  })
})

describe('image checks in the browser', () => {
  it('takes the types and size the server takes', () => {
    expect(imageFileProblem({ type: 'image/png', size: 1 })).toBeNull()
    expect(imageFileProblem({ type: 'image/jpeg', size: MAX_IMAGE_BYTES })).toBeNull()
    expect(imageFileProblem({ type: 'image/webp', size: 1 })).toBeNull()
  })

  it('refuses SVG, which can carry scripts, and anything too large', () => {
    expect(imageFileProblem({ type: 'image/svg+xml', size: 1 })).toBe('admin.upload.wrongType')
    expect(imageFileProblem({ type: 'text/plain', size: 1 })).toBe('admin.upload.wrongType')
    expect(imageFileProblem({ type: 'image/png', size: MAX_IMAGE_BYTES + 1 })).toBe(
      'admin.upload.tooLarge',
    )
  })
})

describe('bulk actions', () => {
  it('finishes the rest when one item fails, and counts both', async () => {
    const seen: number[] = []
    const failure = new Error('no')
    const result = await runBulk([1, 2, 3, 4, 5, 6], async (item) => {
      seen.push(item)

      if (item === 2) {
        throw failure
      }
    })

    expect(seen.sort()).toEqual([1, 2, 3, 4, 5, 6])
    expect(result).toEqual({ succeeded: 5, failed: 1, firstError: failure })
  })
})

describe('small helpers', () => {
  it('builds an address from a name in any of the languages', () => {
    expect(slugify('Pagta köýnek')).toBe('pagta-koynek')
    expect(slugify('Хлопковая рубашка')).toBe('hlopkovaya-rubashka')
    expect(slugify('  --Şäher içi!! ')).toBe('saher-ici')
  })

  it('treats an empty amount as nothing, not zero', () => {
    expect(toAmount('')).toBeNull()
    expect(toAmount('12.50')).toBe(12.5)
    expect(toAmount('0')).toBe(0)
  })

  it('picks the language on screen, then whatever exists', () => {
    const names = [
      { language: 'ru', name: 'Один' },
      { language: 'en', name: 'One' },
    ]

    expect(localizedName(names, 'en')).toBe('One')
    expect(localizedName(names, 'tk')).toBe('Один')
    expect(localizedName([], 'tk')).toBe('')
  })

  it('offers only the moves the order workflow allows', () => {
    expect(nextStatuses('pending')).toEqual(['confirmed', 'cancelled'])
    expect(nextStatuses('packed')).toEqual(['shipped', 'cancelled'])
    expect(nextStatuses('delivered')).toEqual(['returned'])
    expect(nextStatuses('cancelled')).toEqual([])
    expect(nextStatuses('no-such-status')).toEqual([])
  })
})
