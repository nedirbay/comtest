<script setup lang="ts">
import { unwrap, type Schema } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { AppField } from '@comtest/ui'
import { useI18n } from 'vue-i18n'
import { z } from '@comtest/ui/zod'
import { ASSIGNABLE_ROLES } from '@/app/permissions'
import FormDrawer from '@/shared/components/FormDrawer.vue'
import { useAdminForm } from '@/shared/composables/useAdminForm'
import { toastSuccess } from '@/shared/toast'

const emit = defineEmits<{ closed: []; invited: [invitation: Schema<'StaffInvitationDto'>] }>()
const open = defineModel<boolean>({ required: true })

const { t } = useI18n({ useScope: 'global' })
const api = useApi()

const form = useAdminForm({
  schema: z.object({
    email: z.email('validation.email').max(254, 'validation.tooLong'),
    role: z.enum(ASSIGNABLE_ROLES),
  }),
  initial: { email: '', role: 'Staff' as (typeof ASSIGNABLE_ROLES)[number] },
  submit: async (values) => {
    const invitation = await unwrap(
      api.client.POST('/api/v1/admin/staff/invitations', { body: values }),
    )

    emit('invited', invitation)
    toastSuccess(t('admin.staff.invited', { email: values.email }))
    open.value = false
  },
})
</script>

<template>
  <FormDrawer
    v-model="open"
    :title="t('admin.staff.invite')"
    :error="form.formError.value"
    :submitting="form.submitting.value"
    :submit-text="t('admin.staff.sendInvite')"
    @submit="form.handleSubmit()"
    @closed="emit('closed')"
  >
    <AppField name="email" :label="t('admin.customers.email')">
      <el-input
        v-model="form.values.email"
        type="email"
        maxlength="254"
        data-testid="invite-email"
      />
    </AppField>
    <AppField name="role" :label="t('admin.staff.role')">
      <el-select v-model="form.values.role" data-testid="invite-role">
        <el-option
          v-for="role in ASSIGNABLE_ROLES"
          :key="role"
          :value="role"
          :label="t(`admin.roles.${role}`)"
        />
      </el-select>
    </AppField>
  </FormDrawer>
</template>
