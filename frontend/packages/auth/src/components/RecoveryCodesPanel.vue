<script setup lang="ts">
import { AppButton } from '@comtest/ui'
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'

const props = defineProps<{ codes: string[] }>()
defineEmits<{ done: [] }>()

const { t } = useI18n({ useScope: 'global' })
const copied = ref(false)
const saved = ref(false)

async function copy(): Promise<void> {
  await navigator.clipboard.writeText(props.codes.join('\n'))
  copied.value = true
}

function download(): void {
  const url = URL.createObjectURL(new Blob([props.codes.join('\n')], { type: 'text/plain' }))
  const link = document.createElement('a')

  link.href = url
  link.download = 'comtest-recovery-codes.txt'
  link.click()
  URL.revokeObjectURL(url)
}
</script>

<template>
  <div>
    <el-alert
      :title="t('auth.recovery.warning')"
      type="warning"
      show-icon
      :closable="false"
      class="mb-4"
    />
    <ul
      class="mb-4 grid grid-cols-2 gap-x-6 gap-y-1.5 rounded-control border border-line bg-canvas p-4 font-mono text-sm"
      data-testid="recovery-codes"
    >
      <li v-for="code in codes" :key="code">{{ code }}</li>
    </ul>
    <div class="mb-5 flex flex-wrap gap-2">
      <AppButton @click="copy">{{ copied ? t('common.copied') : t('common.copy') }}</AppButton>
      <AppButton @click="download">{{ t('common.download') }}</AppButton>
    </div>
    <el-checkbox v-model="saved" class="mb-4">{{ t('auth.recovery.saved') }}</el-checkbox>
    <AppButton variant="primary" block :disabled="!saved" @click="$emit('done')">
      {{ t('common.continue') }}
    </AppButton>
  </div>
</template>
