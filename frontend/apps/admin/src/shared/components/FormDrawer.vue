<script setup lang="ts">
import { AppButton, AppForm } from '@comtest/ui'
import { useI18n } from 'vue-i18n'

/**
 * The frame of every side-panel form. The form's fields go in the slot. The component using it
 * calls `useAdminForm` itself, so that the fields find their errors, and is mounted only while
 * the drawer is open, which gives each opening fresh values.
 */
withDefaults(
  defineProps<{
    title: string
    error?: string | null
    submitting?: boolean
    submitText?: string
    readonly?: boolean
    /** A number of rem, capped at the screen width on phones. */
    width?: number
  }>(),
  { width: 34 },
)

defineEmits<{ submit: []; closed: [] }>()

const open = defineModel<boolean>({ required: true })
const { t } = useI18n({ useScope: 'global' })
</script>

<template>
  <el-drawer
    v-model="open"
    :title="title"
    :size="`min(${width}rem, 100vw)`"
    direction="rtl"
    append-to-body
    @closed="$emit('closed')"
  >
    <AppForm :error="error" @submit="$emit('submit')">
      <slot />
      <!-- Pulled down over the drawer's padding, or the fields would show through underneath it. -->
      <div
        class="sticky bottom-[calc(var(--el-drawer-padding-primary,20px)*-1)] -mx-1 mt-4 flex justify-end gap-2 bg-surface px-1 pt-3 pb-5"
      >
        <AppButton @click="open = false">{{ t('common.cancel') }}</AppButton>
        <AppButton
          v-if="!readonly"
          variant="primary"
          type="submit"
          :loading="submitting"
          data-testid="drawer-submit"
        >
          {{ submitText ?? t('common.save') }}
        </AppButton>
      </div>
    </AppForm>
  </el-drawer>
</template>
