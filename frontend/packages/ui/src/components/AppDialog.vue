<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import AppButton from './AppButton.vue'

withDefaults(
  defineProps<{
    title: string
    confirmText?: string
    confirmVariant?: 'primary' | 'danger'
    loading?: boolean
    width?: string
  }>(),
  { confirmVariant: 'primary', width: 'min(32rem, calc(100vw - 2rem))' },
)

defineEmits<{ confirm: [] }>()

const open = defineModel<boolean>({ required: true })
const { t } = useI18n({ useScope: 'global' })
</script>

<template>
  <el-dialog v-model="open" :title="title" :width="width" align-center destroy-on-close>
    <slot />
    <template #footer>
      <div class="flex justify-end gap-2">
        <slot name="actions">
          <AppButton @click="open = false">{{ t('common.cancel') }}</AppButton>
          <AppButton :variant="confirmVariant" :loading="loading" @click="$emit('confirm')">
            {{ confirmText ?? t('common.confirm') }}
          </AppButton>
        </slot>
      </div>
    </template>
  </el-dialog>
</template>
