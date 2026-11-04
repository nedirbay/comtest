<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi, useAuthStore } from '@comtest/auth'
import { AppDialog, AppTable, PageHeader, useApiErrorMessage, type TableColumn } from '@comtest/ui'
import { Check, Plus } from '@element-plus/icons-vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ASSIGNABLE_ROLES, ROLE_PERMISSIONS, STORE_PERMISSIONS } from '@/app/permissions'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useDrawerItem } from '@/shared/composables/useDrawerItem'
import { useFormat } from '@/shared/composables/useFormat'
import { toastSuccess } from '@/shared/toast'
import InviteForm from '../components/InviteForm.vue'

type Member = Schema<'StaffMemberDto'>
type Invitation = Schema<'StaffInvitationDto'>

const STAFF_KEY = ['admin', 'staff', 'members']
const INVITATIONS_KEY = ['admin', 'staff', 'invitations']

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const auth = useAuthStore()
const queryClient = useQueryClient()
const confirm = useConfirm()
const format = useFormat()
const describeError = useApiErrorMessage()

const members = useQuery({
  queryKey: STAFF_KEY,
  queryFn: () => unwrap(api.client.GET('/api/v1/admin/staff')),
})

const memberColumns = computed<TableColumn[]>(() => [
  { key: 'fullName', label: t('admin.common.name'), minWidth: 170 },
  { key: 'email', label: t('admin.customers.email'), minWidth: 290 },
  { key: 'role', label: t('admin.staff.role'), minWidth: 120 },
  { key: 'joinedAt', label: t('admin.staff.joinedAt'), minWidth: 170 },
  { key: 'actions', label: '', width: 200, align: 'right' },
])

const canChange = (member: Member) =>
  member.role !== 'StoreOwner' && member.userId !== auth.claims?.userId

const roleLabel = (role: string) => t(`admin.roles.${role}`)

const inviting = useDrawerItem<never>()

const invitations = useQuery({
  queryKey: INVITATIONS_KEY,
  queryFn: () => unwrap(api.client.GET('/api/v1/admin/staff/invitations')),
})

const invitationColumns = computed<TableColumn[]>(() => [
  { key: 'email', label: t('admin.customers.email'), minWidth: 290 },
  { key: 'role', label: t('admin.staff.role'), minWidth: 120 },
  { key: 'expiresAt', label: t('admin.staff.expiresAt'), minWidth: 180 },
  { key: 'actions', label: '', minWidth: 110, align: 'right' },
])

function revoke(invitation: Invitation): void {
  confirm.ask({
    title: t('admin.staff.revoke'),
    message: t('admin.staff.revokeConfirm', { email: invitation.email }),
    confirmText: t('admin.staff.revoke'),
    danger: true,
    action: async () => {
      await unwrap(
        api.client.DELETE('/api/v1/admin/staff/invitations/{id}', {
          params: { path: { id: invitation.id } },
        }),
      )
      await queryClient.invalidateQueries({ queryKey: INVITATIONS_KEY })
      toastSuccess(t('admin.staff.revoked'))
    },
  })
}

function removeMember(member: Member): void {
  confirm.ask({
    title: t('admin.staff.remove'),
    message: t('admin.staff.removeConfirm', { name: member.fullName }),
    confirmText: t('admin.staff.remove'),
    danger: true,
    action: async () => {
      await unwrap(
        api.client.DELETE('/api/v1/admin/staff/{userId}', {
          params: { path: { userId: member.userId } },
        }),
      )
      await queryClient.invalidateQueries({ queryKey: STAFF_KEY })
      toastSuccess(t('admin.staff.removed'))
    },
  })
}

const roleTarget = ref<Member | null>(null)
const newRole = ref<string>('Staff')
const roleError = ref<string | null>(null)

const roleDialogOpen = computed<boolean>({
  get: () => roleTarget.value !== null,
  set: (value) => {
    if (!value) {
      roleTarget.value = null
    }
  },
})

function openRoleDialog(member: Member): void {
  roleTarget.value = member
  newRole.value = member.role
  roleError.value = null
}

const changeRole = useMutation({
  mutationFn: () =>
    unwrap(
      api.client.PUT('/api/v1/admin/staff/{userId}/role', {
        params: { path: { userId: (roleTarget.value as Member).userId } },
        body: { role: newRole.value },
      }),
    ),
  onSuccess: async () => {
    await queryClient.invalidateQueries({ queryKey: STAFF_KEY })
    roleTarget.value = null
    toastSuccess(t('admin.staff.roleChanged'))
  },
  onError: (error) => {
    roleError.value = describeError(error)
  },
})

const matrixColumns = computed<TableColumn[]>(() => [
  { key: 'label', label: t('admin.staff.permission'), minWidth: 230 },
  ...(['StoreOwner', 'Manager', 'Staff'] as const).map((role) => ({
    key: role,
    label: roleLabel(role),
    minWidth: 110,
    align: 'center' as const,
  })),
])

const matrixRows = computed(() =>
  STORE_PERMISSIONS.map((permission) => ({
    id: permission,
    label: t(`admin.permissions.${permission.replaceAll('.', '_')}`),
  })),
)

const has = (role: keyof typeof ROLE_PERMISSIONS, permission: string) =>
  ROLE_PERMISSIONS[role].includes(permission)
</script>

<template>
  <PageHeader :title="t('admin.nav.staff')" :description="t('admin.staff.intro')">
    <template #actions>
      <el-button type="primary" :icon="Plus" data-testid="invite-staff" @click="inviting.show()">
        {{ t('admin.staff.invite') }}
      </el-button>
    </template>
  </PageHeader>

  <AppTable
    row-key="userId"
    :columns="memberColumns"
    :rows="members.data.value ?? []"
    :loading="members.isPending.value"
    :error="members.error.value"
    :empty-title="t('admin.staff.empty')"
    @retry="members.refetch()"
  >
    <template #cell-fullName="{ row }">
      <span class="font-semibold text-ink" data-testid="staff-row">{{ row.fullName }}</span>
    </template>
    <template #cell-role="{ row }">{{ roleLabel(row.role) }}</template>
    <template #cell-joinedAt="{ row }">{{ format.dateTime(row.joinedAt) }}</template>
    <template #cell-actions="{ row }">
      <template v-if="canChange(row)">
        <el-button size="small" data-testid="staff-role" @click="openRoleDialog(row)">
          {{ t('admin.staff.changeRole') }}
        </el-button>
        <el-button
          size="small"
          type="danger"
          plain
          data-testid="staff-remove"
          @click="removeMember(row)"
        >
          {{ t('admin.staff.remove') }}
        </el-button>
      </template>
    </template>
  </AppTable>

  <section class="mt-6">
    <h2 class="font-bold text-ink">{{ t('admin.staff.invitations') }}</h2>
    <p class="mb-3 text-sm text-ink-muted">{{ t('admin.staff.invitationsHint') }}</p>
    <AppTable
      :columns="invitationColumns"
      :rows="invitations.data.value ?? []"
      :loading="invitations.isPending.value"
      :error="invitations.error.value"
      :empty-title="t('admin.staff.noInvitations')"
      data-testid="invitations"
      @retry="invitations.refetch()"
    >
      <template #cell-email="{ row }">
        <span data-testid="invitation-row">{{ row.email }}</span>
      </template>
      <template #cell-role="{ row }">{{ roleLabel(row.role) }}</template>
      <template #cell-expiresAt="{ row }">{{ format.dateTime(row.expiresAt) }}</template>
      <template #cell-actions="{ row }">
        <el-button
          size="small"
          type="danger"
          plain
          data-testid="invitation-revoke"
          @click="revoke(row)"
        >
          {{ t('admin.staff.revoke') }}
        </el-button>
      </template>
    </AppTable>
  </section>

  <section class="mt-6">
    <h2 class="font-bold text-ink">{{ t('admin.staff.matrix') }}</h2>
    <p class="mb-3 text-sm text-ink-muted">{{ t('admin.staff.matrixHint') }}</p>
    <AppTable :columns="matrixColumns" :rows="matrixRows" data-testid="permission-matrix">
      <template #cell-StoreOwner="{ row }">
        <el-icon
          v-if="has('StoreOwner', row.id)"
          class="text-success"
          role="img"
          :aria-label="t('admin.common.yes')"
        >
          <Check />
        </el-icon>
        <span v-else class="text-ink-subtle" role="img" :aria-label="t('admin.common.no')">—</span>
      </template>
      <template #cell-Manager="{ row }">
        <el-icon
          v-if="has('Manager', row.id)"
          class="text-success"
          role="img"
          :aria-label="t('admin.common.yes')"
        >
          <Check />
        </el-icon>
        <span v-else class="text-ink-subtle" role="img" :aria-label="t('admin.common.no')">—</span>
      </template>
      <template #cell-Staff="{ row }">
        <el-icon
          v-if="has('Staff', row.id)"
          class="text-success"
          role="img"
          :aria-label="t('admin.common.yes')"
        >
          <Check />
        </el-icon>
        <span v-else class="text-ink-subtle" role="img" :aria-label="t('admin.common.no')">—</span>
      </template>
    </AppTable>
  </section>

  <InviteForm
    v-if="inviting.mounted.value"
    :key="inviting.session.value"
    v-model="inviting.open.value"
    @invited="queryClient.invalidateQueries({ queryKey: INVITATIONS_KEY })"
    @closed="inviting.closed()"
  />

  <AppDialog
    v-model="roleDialogOpen"
    :title="t('admin.staff.changeRole')"
    :loading="changeRole.isPending.value"
    @confirm="changeRole.mutate()"
  >
    <p class="mb-3 text-sm text-ink">{{ roleTarget?.fullName }}</p>
    <el-select
      v-model="newRole"
      :aria-label="t('admin.staff.role')"
      data-testid="staff-role-select"
    >
      <el-option
        v-for="role in ASSIGNABLE_ROLES"
        :key="role"
        :value="role"
        :label="roleLabel(role)"
      />
    </el-select>
    <p v-if="roleError" class="mt-2 text-sm text-danger" role="alert">{{ roleError }}</p>
  </AppDialog>
</template>
