<script setup lang="ts">
import { unwrap } from '@comtest/api-client'
import { useApi } from '@comtest/auth'
import { ErrorState, SkeletonBlock } from '@comtest/ui'
import { useQuery } from '@tanstack/vue-query'
import PaymentSettingsForm from './PaymentSettingsForm.vue'

const api = useApi()

const settings = useQuery({
  queryKey: ['admin', 'store', 'payment-settings'],
  queryFn: () => unwrap(api.client.GET('/api/v1/admin/store/payment-settings')),
})
</script>

<template>
  <ErrorState
    v-if="settings.isError.value"
    :error="settings.error.value"
    retryable
    @retry="settings.refetch()"
  />
  <SkeletonBlock v-else-if="settings.isPending.value" :lines="6" />
  <PaymentSettingsForm
    v-else-if="settings.data.value"
    :settings="settings.data.value"
    @saved="settings.refetch()"
  />
</template>
