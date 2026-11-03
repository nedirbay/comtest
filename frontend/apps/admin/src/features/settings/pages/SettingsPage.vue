<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { ErrorState, PageHeader, SkeletonBlock } from '@comtest/ui'
import { useQuery } from '@tanstack/vue-query'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import GeneralSettings from '../components/GeneralSettings.vue'
import LanguageSettings from '../components/LanguageSettings.vue'
import PaymentSettings from '../components/PaymentSettings.vue'
import ShippingSettings from '../components/ShippingSettings.vue'

const TABS = ['general', 'payment', 'shipping', 'languages'] as const

const { t } = useI18n({ useScope: 'global' })
const api = useApi()
const route = useRoute()
const router = useRouter()

const store = useQuery({
  queryKey: ['admin', 'store', 'settings'],
  queryFn: () => unwrap(api.client.GET('/api/v1/admin/store')),
})

const tab = computed<string>({
  get: () => {
    const wanted = route.query.tab

    return TABS.includes(wanted as (typeof TABS)[number]) ? String(wanted) : 'general'
  },
  set: (value) => {
    void router.replace({ query: { ...route.query, tab: value } })
  },
})
</script>

<template>
  <PageHeader :title="t('admin.nav.settings')" :description="t('admin.settings.intro')" />

  <div class="rounded-card border border-line bg-surface p-3 sm:p-5">
    <ErrorState
      v-if="store.isError.value"
      :error="store.error.value"
      retryable
      @retry="store.refetch()"
    />
    <SkeletonBlock v-else-if="store.isPending.value" :lines="8" />
    <el-tabs v-else-if="store.data.value" v-model="tab" data-testid="settings-tabs">
      <el-tab-pane :label="t('admin.settings.tabGeneral')" name="general" lazy>
        <GeneralSettings :store="store.data.value" @reload="store.refetch()" />
      </el-tab-pane>
      <el-tab-pane :label="t('admin.settings.tabPayment')" name="payment" lazy>
        <PaymentSettings />
      </el-tab-pane>
      <el-tab-pane :label="t('admin.settings.tabShipping')" name="shipping" lazy>
        <ShippingSettings />
      </el-tab-pane>
      <el-tab-pane :label="t('admin.settings.tabLanguages')" name="languages" lazy>
        <LanguageSettings :store="store.data.value" />
      </el-tab-pane>
    </el-tabs>
  </div>
</template>
