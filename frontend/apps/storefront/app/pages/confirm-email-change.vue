<script setup lang="ts">
import { ConfirmEmailChangePanel } from '@comtest/auth'
import { AppButton, SkeletonBlock } from '@comtest/ui'

definePageMeta({ layout: 'auth' })

const { t } = useI18n()
const localePath = useLocalePath()

useHead({ title: computed(() => t('auth.emailChange.confirmTitle')) })
</script>

<template>
  <h1 class="mb-4 text-2xl font-bold tracking-tight text-ink">
    {{ t('auth.emailChange.confirmTitle') }}
  </h1>
  <ClientOnly>
    <ConfirmEmailChangePanel>
      <template #login>
        <NuxtLink :to="localePath('/login')">
          <AppButton variant="primary">{{ t('auth.login.submit') }}</AppButton>
        </NuxtLink>
      </template>
    </ConfirmEmailChangePanel>
    <template #fallback><SkeletonBlock :lines="3" /></template>
  </ClientOnly>
</template>
