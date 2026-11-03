<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { ApiError } from '@comtest/api-client'
import { useApiErrorMessage } from '../composables/useApiErrorMessage'
import AppButton from './AppButton.vue'

const props = defineProps<{ error?: unknown; title?: string; retryable?: boolean }>()
defineEmits<{ retry: [] }>()

const { t } = useI18n({ useScope: 'global' })
const describe = useApiErrorMessage()

const correlationId = computed(() =>
  props.error instanceof ApiError ? props.error.correlationId : undefined,
)
</script>

<template>
  <div class="flex flex-col items-center gap-2 px-6 py-12 text-center" role="alert">
    <p class="text-base font-semibold text-ink">{{ title ?? t('states.errorTitle') }}</p>
    <p class="max-w-md text-sm text-ink-muted">{{ describe(error) }}</p>
    <p v-if="correlationId" class="text-xs text-ink-subtle">
      {{ t('states.referenceCode', { code: correlationId }) }}
    </p>
    <AppButton v-if="retryable" class="mt-3" @click="$emit('retry')">
      {{ t('common.retry') }}
    </AppButton>
  </div>
</template>
