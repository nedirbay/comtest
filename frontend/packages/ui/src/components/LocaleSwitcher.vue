<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { LOCALE_NAMES, SUPPORTED_LOCALES, type AppLocale } from '../locale'

defineProps<{ modelValue: string }>()
defineEmits<{ 'update:modelValue': [locale: AppLocale] }>()

const { t } = useI18n({ useScope: 'global' })
</script>

<template>
  <el-dropdown trigger="click" @command="$emit('update:modelValue', $event)">
    <button
      type="button"
      class="inline-flex h-9 items-center gap-1 rounded-control px-2.5 text-sm font-semibold text-ink-muted hover:bg-primary-soft hover:text-ink"
      :aria-label="`${t('common.language')}: ${modelValue.toUpperCase()}`"
    >
      {{ modelValue.toUpperCase() }}
    </button>
    <template #dropdown>
      <el-dropdown-menu>
        <el-dropdown-item
          v-for="locale in SUPPORTED_LOCALES"
          :key="locale"
          :command="locale"
          :disabled="locale === modelValue"
        >
          <span :lang="locale">{{ LOCALE_NAMES[locale] }}</span>
        </el-dropdown-item>
      </el-dropdown-menu>
    </template>
  </el-dropdown>
</template>
