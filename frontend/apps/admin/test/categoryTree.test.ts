import type { Schema } from '@comtest/api-client'
import { describe, expect, it } from 'vitest'
import {
  buildTree,
  planMove,
  subtreeIds,
  toSaveRequest,
} from '../src/features/catalog/categoryTree'

type Category = Schema<'CategoryDto'>

function category(id: string, parentId: string | null, sortOrder = 0): Category {
  return {
    id,
    parentId,
    slug: id,
    sortOrder,
    isActive: true,
    translations: [{ language: 'tk', name: id.toUpperCase(), description: null }],
    version: 1,
  }
}

const list = [
  category('a', null, 0),
  category('b', 'a', 0),
  category('c', 'b', 0),
  category('d', null, 10),
]

describe('the category tree', () => {
  it('nests children under their parents and keeps orphans visible', () => {
    const tree = buildTree([...list, category('lost', 'missing')], (item) => item.slug)

    expect(tree.map((node) => node.id)).toEqual(['a', 'd', 'lost'])
    expect(tree[0]?.children[0]?.children[0]?.id).toBe('c')
  })

  it('finds a category with everything under it', () => {
    expect([...subtreeIds(list, 'a')].sort()).toEqual(['a', 'b', 'c'])
    expect([...subtreeIds(list, 'd')]).toEqual(['d'])
  })

  it('saves only what a move really changed', () => {
    expect(planMove(['a', 'd'], null, list)).toEqual([])

    const reordered = planMove(['d', 'a'], null, list)

    expect(reordered.map((move) => [move.category.id, move.sortOrder])).toEqual([
      ['d', 0],
      ['a', 10],
    ])

    const moved = planMove(['b', 'd'], 'a', list)

    expect(moved).toEqual([expect.objectContaining({ parentId: 'a', sortOrder: 10 })])
    expect(moved[0]?.category.id).toBe('d')
  })

  it('keeps everything else about a category when saving a move', () => {
    const request = toSaveRequest(category('x', null), { parentId: 'p', sortOrder: 30 })

    expect(request).toMatchObject({
      slug: 'x',
      parentId: 'p',
      sortOrder: 30,
      version: 1,
      isActive: true,
      translations: [{ language: 'tk', name: 'X' }],
    })
  })
})
