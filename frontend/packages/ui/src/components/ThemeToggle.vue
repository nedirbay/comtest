<script setup lang="ts">
import { Moon, Sunny } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'

defineProps<{ isDark: boolean }>()
defineEmits<{ toggle: [] }>()

const { t } = useI18n({ useScope: 'global' })
</script>

<template>
  <button
    type="button"
    class="inline-flex size-9 items-center justify-center rounded-control text-ink-muted hover:bg-primary-soft hover:text-ink"
    :aria-label="isDark ? t('common.themeLight') : t('common.themeDark')"
    @click="$emit('toggle')"
  >
    <el-icon :size="18">
      <Sunny v-if="isDark" />
      <Moon v-else />
    </el-icon>
  </button>
</template>
