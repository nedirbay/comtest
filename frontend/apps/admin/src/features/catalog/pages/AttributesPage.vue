<script setup lang="ts">
import { useAuthStore } from '@comtest/auth'
import { AppTable, PageHeader, type TableColumn } from '@comtest/ui'
import { Plus } from '@element-plus/icons-vue'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { Permission } from '@/app/permissions'
import { useDrawerItem } from '@/shared/composables/useDrawerItem'
import { localizedName } from '@/shared/localized'
import AttributeForm from '../components/AttributeForm.vue'
import { useAttributes } from '../queries'

const { t, locale } = useI18n({ useScope: 'global' })
const auth = useAuthStore()
const attributes = useAttributes()
const drawer = useDrawerItem<never>()

const canWrite = computed(() => auth.can(Permission.CatalogWrite))

const columns = computed<TableColumn[]>(() => [
  { key: 'name', label: t('admin.common.name'), minWidth: 200 },
  { key: 'code', label: t('admin.attributes.code'), minWidth: 160 },
  { key: 'dataType', label: t('admin.attributes.dataType'), minWidth: 130 },
  { key: 'options', label: t('admin.attributes.options'), minWidth: 220 },
])
</script>

<template>
  <PageHeader :title="t('admin.nav.attributes')" :description="t('admin.attributes.intro')">
    <template #actions>
      <el-button
        v-if="canWrite"
        type="primary"
        :icon="Plus"
        data-testid="new-attribute"
        @click="drawer.show()"
      >
        {{ t('admin.attributes.new') }}
      </el-button>
    </template>
  </PageHeader>

  <AppTable
    :columns="columns"
    :rows="attributes.data.value ?? []"
    :loading="attributes.isPending.value"
    :error="attributes.error.value"
    :empty-title="t('admin.attributes.empty')"
    @retry="attributes.refetch()"
  >
    <template #cell-name="{ row }">
      <span class="font-semibold text-ink" data-testid="attribute-row">
        {{ localizedName(row.translations, locale) || row.code }}
      </span>
    </template>
    <template #cell-dataType="{ row }">
      {{ t(`admin.attributes.type_${row.dataType}`) }}
    </template>
    <template #cell-options="{ row }">{{ row.options.join(', ') }}</template>
  </AppTable>

  <AttributeForm
    v-if="drawer.mounted.value"
    :key="drawer.session.value"
    v-model="drawer.open.value"
    @closed="drawer.closed()"
  />
</template>
