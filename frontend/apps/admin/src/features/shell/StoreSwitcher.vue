<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { useApi, useAuthStore } from '@comtest/auth'
import { useApiErrorMessage } from '@comtest/ui'
import { ArrowDown, Shop } from '@element-plus/icons-vue'
import { useQuery, useQueryClient } from '@tanstack/vue-query'
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { toastError } from '@/shared/toast'

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const router = useRouter()
const queryClient = useQueryClient()
const describeError = useApiErrorMessage()
const switching = ref(false)

const memberships = useQuery({
  queryKey: ['account', 'tenants'],
  queryFn: () => unwrap(api.client.GET('/api/v1/account/tenants')),
})

const currentStore = computed(() =>
  memberships.data.value?.find((store) => store.tenantId === auth.claims?.tenantId),
)

const canSwitch = computed(() => (memberships.data.value?.length ?? 0) > 1)

async function choose(tenantId: string): Promise<void> {
  if (tenantId === auth.claims?.tenantId || switching.value) {
    return
  }

  // Leaving first gives the unsaved-changes guard a say before the store changes under the form.
  await router.push({ name: 'dashboard' })

  if (router.currentRoute.value.name !== 'dashboard') {
    return
  }

  switching.value = true

  try {
    await auth.switchTenant(tenantId)
    // Nothing of the previous store may stay on screen. The layout remounts on the new store's id.
    queryClient.clear()
  } catch (error) {
    toastError(describeError(error))
  } finally {
    switching.value = false
  }
}
</script>

<template>
  <div v-if="currentStore" class="min-w-0">
    <el-dropdown v-if="canSwitch" trigger="click" class="w-full" @command="choose">
      <button
        type="button"
        class="flex w-full items-center gap-2 rounded-control border border-line px-3 py-2 text-left text-sm font-semibold text-ink hover:border-line-strong"
        :aria-label="t('admin.shell.switchStore')"
        :disabled="switching"
        data-testid="store-switcher"
      >
        <el-icon class="shrink-0 text-primary"><Shop /></el-icon>
        <span class="min-w-0 flex-1 truncate">{{ currentStore.tenantName }}</span>
        <el-icon class="shrink-0 text-ink-subtle"><ArrowDown /></el-icon>
      </button>
      <template #dropdown>
        <el-dropdown-menu>
          <el-dropdown-item
            v-for="store in memberships.data.value"
            :key="store.tenantId"
            :command="store.tenantId"
            :disabled="store.tenantId === auth.claims?.tenantId"
            :data-testid="`store-${store.tenantSlug}`"
          >
            {{ store.tenantName }}
          </el-dropdown-item>
        </el-dropdown-menu>
      </template>
    </el-dropdown>
    <p
      v-else
      class="flex items-center gap-2 rounded-control px-3 py-2 text-sm font-semibold text-ink"
      data-testid="store-name"
    >
      <el-icon class="shrink-0 text-primary"><Shop /></el-icon>
      <span class="min-w-0 truncate">{{ currentStore.tenantName }}</span>
    </p>
  </div>
</template>
