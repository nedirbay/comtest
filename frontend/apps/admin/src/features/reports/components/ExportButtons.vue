<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { useApiErrorMessage } from '@comtest/ui'
import { Download } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { STORE_OFFSET_MINUTES } from '@/features/dashboard/days'

const props = defineProps<{
  kind: 'sales' | 'products' | 'customers' | 'stock'
  from?: string
  to?: string
}>()

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const errorMessage = useApiErrorMessage()
const busy = ref<'csv' | 'xlsx' | null>(null)

async function download(format: 'csv' | 'xlsx'): Promise<void> {
  busy.value = format

  try {
    const file = await unwrap(
      api.client.GET('/api/v1/admin/reports/export', {
        params: {
          query: {
            kind: props.kind,
            format,
            from: props.from,
            to: props.to,
            utcOffsetMinutes: STORE_OFFSET_MINUTES,
          },
        },
        parseAs: 'blob',
      }),
    )

    const url = URL.createObjectURL(file as unknown as Blob)
    const link = document.createElement('a')

    link.href = url
    link.download = `${props.kind}.${format}`
    link.click()
    URL.revokeObjectURL(url)
  } catch (error) {
    ElMessage.error(errorMessage(error))
  } finally {
    busy.value = null
  }
}
</script>

<template>
  <div class="flex gap-2">
    <el-button
      :icon="Download"
      :loading="busy === 'csv'"
      :data-testid="`export-${kind}-csv`"
      @click="download('csv')"
    >
      CSV
    </el-button>
    <el-button
      :icon="Download"
      :loading="busy === 'xlsx'"
      :aria-label="t('admin.reports.exportExcel')"
      :data-testid="`export-${kind}-xlsx`"
      @click="download('xlsx')"
    >
      Excel
    </el-button>
  </div>
</template>
