import type { Schema } from '@comtest/api-client'

type Category = Schema<'CategoryDto'>

export interface CategoryNode {
  id: string
  label: string
  category: Category
  children: CategoryNode[]
}

/**
 * Builds the nested tree the component draws. It gets a fresh copy each time, because `el-tree`
 * rearranges its data in place during a drag, and the query cache must stay what the server said.
 */
export function buildTree(categories: readonly Category[], labelOf: (item: Category) => string) {
  const nodes = new Map<string, CategoryNode>(
    categories.map((category) => [
      category.id,
      { id: category.id, label: labelOf(category), category, children: [] },
    ]),
  )
  const roots: CategoryNode[] = []

  for (const category of categories) {
    const node = nodes.get(category.id) as CategoryNode
    const parent = category.parentId ? nodes.get(category.parentId) : undefined

    if (parent) {
      parent.children.push(node)
    } else {
      roots.push(node)
    }
  }

  return roots
}

/** This category and everything under it: the places it cannot be moved to. */
export function subtreeIds(categories: readonly Category[], id: string): Set<string> {
  const found = new Set<string>([id])
  let grew = true

  while (grew) {
    grew = false

    for (const category of categories) {
      if (category.parentId && found.has(category.parentId) && !found.has(category.id)) {
        found.add(category.id)
        grew = true
      }
    }
  }

  return found
}

const SORT_STEP = 10

export interface PlannedMove {
  category: Category
  parentId: string | null
  sortOrder: number
}

/**
 * What must be saved after a drop: the siblings in their new order. Only what actually changed goes
 * in the list, as each entry costs a request and takes a new version on the server.
 */
export function planMove(
  siblingIds: readonly string[],
  parentId: string | null,
  categories: readonly Category[],
): PlannedMove[] {
  const byId = new Map(categories.map((category) => [category.id, category]))
  const moves: PlannedMove[] = []

  siblingIds.forEach((id, index) => {
    const category = byId.get(id)
    const sortOrder = index * SORT_STEP

    if (category && (category.parentId !== parentId || category.sortOrder !== sortOrder)) {
      moves.push({ category, parentId, sortOrder })
    }
  })

  return moves
}

export function toSaveRequest(
  category: Category,
  changes: { parentId: string | null; sortOrder: number },
): Schema<'SaveCategoryRequest'> {
  return {
    slug: category.slug,
    isActive: category.isActive,
    version: category.version,
    translations: category.translations.map((item) => ({
      language: item.language,
      name: item.name,
      description: item.description,
    })),
    ...changes,
  }
}
