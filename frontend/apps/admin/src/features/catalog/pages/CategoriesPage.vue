<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi, useAuthStore } from '@comtest/auth'
import { EmptyState, ErrorState, PageHeader, SkeletonBlock, useApiErrorMessage } from '@comtest/ui'
import { Delete, Edit, Plus } from '@element-plus/icons-vue'
import { useQueryClient } from '@tanstack/vue-query'
import type Node from 'element-plus/es/components/tree/src/model/node'
import { computed, ref, shallowRef, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { Permission } from '@/app/permissions'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useDrawerItem } from '@/shared/composables/useDrawerItem'
import { localizedName } from '@/shared/localized'
import { toastError, toastSuccess } from '@/shared/toast'
import { buildTree, planMove, toSaveRequest, type CategoryNode } from '../categoryTree'
import CategoryForm from '../components/CategoryForm.vue'
import { CATEGORIES_KEY, useCategories } from '../queries'

type Category = Schema<'CategoryDto'>

const { t, locale } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const queryClient = useQueryClient()
const confirm = useConfirm()
const describeError = useApiErrorMessage()

const categories = useCategories()
const canWrite = computed(() => auth.can(Permission.CatalogWrite))
const drawer = useDrawerItem<Category>()
const newParent = ref<string | null>(null)
const saving = ref(false)

const tree = shallowRef<CategoryNode[]>([])

watch(
  [() => categories.data.value, locale],
  ([list]) => {
    tree.value = buildTree(list ?? [], (item) => localizedName(item.translations, locale.value))
  },
  { immediate: true },
)

function addChild(parentId: string | null): void {
  newParent.value = parentId
  drawer.show()
}

function remove(category: Category): void {
  confirm.ask({
    title: t('admin.categories.delete'),
    message: t('admin.categories.deleteConfirm', {
      name: localizedName(category.translations, locale.value),
    }),
    confirmText: t('common.delete'),
    danger: true,
    action: async () => {
      await unwrap(
        api.client.DELETE('/api/v1/admin/catalog/categories/{id}', {
          params: { path: { id: category.id } },
        }),
      )
      await queryClient.invalidateQueries({ queryKey: CATEGORIES_KEY })
      toastSuccess(t('admin.categories.deleted'))
    },
  })
}

function allowDrag(): boolean {
  return canWrite.value && !saving.value
}

function allowDrop(): boolean {
  return canWrite.value && !saving.value
}

async function onDrop(_dragged: Node, target: Node, position: string): Promise<void> {
  const parent = position === 'inner' ? target : target.parent
  // The top of the tree is the component's own hidden root, which holds no category.
  const parentId = parent && parent.level > 0 ? (parent.data as CategoryNode).id : null
  const moves = planMove(
    (parent?.childNodes ?? []).map((node) => (node.data as CategoryNode).id),
    parentId,
    categories.data.value ?? [],
  )

  saving.value = true

  try {
    for (const move of moves) {
      await unwrap(
        api.client.PUT('/api/v1/admin/catalog/categories/{id}', {
          params: { path: { id: move.category.id } },
          body: toSaveRequest(move.category, {
            parentId: move.parentId,
            sortOrder: move.sortOrder,
          }),
        }),
      )
    }

    toastSuccess(t('admin.categories.moved'))
  } catch (error) {
    toastError(describeError(error))
  } finally {
    // Also after a failure, as the tree on screen shows a move that did not fully happen.
    await queryClient.invalidateQueries({ queryKey: CATEGORIES_KEY })
    saving.value = false
  }
}
</script>

<template>
  <PageHeader :title="t('admin.nav.categories')" :description="t('admin.categories.intro')">
    <template #actions>
      <el-button
        v-if="canWrite"
        type="primary"
        :icon="Plus"
        data-testid="new-category"
        @click="addChild(null)"
      >
        {{ t('admin.categories.new') }}
      </el-button>
    </template>
  </PageHeader>

  <div class="rounded-card border border-line bg-surface p-2 sm:p-4">
    <ErrorState
      v-if="categories.isError.value"
      :error="categories.error.value"
      retryable
      @retry="categories.refetch()"
    />
    <div v-else-if="categories.isPending.value" class="p-4"><SkeletonBlock :lines="6" /></div>
    <EmptyState v-else-if="tree.length === 0" :title="t('admin.categories.empty')" />
    <el-tree
      v-else
      v-loading="saving"
      :data="tree"
      node-key="id"
      default-expand-all
      :expand-on-click-node="false"
      :draggable="canWrite"
      :allow-drag="allowDrag"
      :allow-drop="allowDrop"
      data-testid="category-tree"
      @node-drop="onDrop"
    >
      <template #default="{ data }">
        <div class="flex min-w-0 flex-1 items-center justify-between gap-2 pr-1 text-sm">
          <span class="min-w-0 truncate font-semibold text-ink" data-testid="category-node">
            {{ data.label }}
            <el-tag v-if="!data.category.isActive" size="small" type="info" class="ml-1">
              {{ t('admin.common.inactive') }}
            </el-tag>
          </span>
          <span class="flex shrink-0 items-center">
            <el-button
              v-if="canWrite"
              :icon="Plus"
              text
              size="small"
              :aria-label="t('admin.categories.addChild')"
              :title="t('admin.categories.addChild')"
              @click.stop="addChild(data.id)"
            />
            <el-button
              :icon="Edit"
              text
              size="small"
              :aria-label="t('admin.common.edit')"
              :title="t('admin.common.edit')"
              data-testid="category-edit"
              @click.stop="drawer.show(data.category)"
            />
            <el-button
              v-if="canWrite"
              :icon="Delete"
              text
              size="small"
              type="danger"
              :aria-label="t('common.delete')"
              :title="t('common.delete')"
              data-testid="category-delete"
              @click.stop="remove(data.category)"
            />
          </span>
        </div>
      </template>
    </el-tree>
    <p
      v-if="canWrite && tree.length"
      class="mt-3 border-t border-line px-2 pt-2 text-xs text-ink-subtle"
    >
      {{ t('admin.categories.dragHint') }}
    </p>
  </div>

  <CategoryForm
    v-if="drawer.mounted.value"
    :key="drawer.session.value"
    v-model="drawer.open.value"
    :category="drawer.item.value"
    :parent-id="newParent"
    :categories="categories.data.value ?? []"
    @closed="drawer.closed()"
  />
</template>
