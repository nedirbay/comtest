<script setup lang="ts">
import { ConfirmEmailPanel } from '@comtest/auth'
import { AppButton, SkeletonBlock } from '@comtest/ui'

definePageMeta({ layout: 'auth' })

const { t } = useI18n()
const localePath = useLocalePath()

useHead({ title: computed(() => t('auth.confirm.title')) })
</script>

<template>
  <h1 class="mb-4 text-2xl font-bold tracking-tight text-ink">{{ t('auth.confirm.title') }}</h1>
  <ClientOnly>
    <ConfirmEmailPanel>
      <template #login>
        <NuxtLink :to="localePath('/login')">
          <AppButton variant="primary">{{ t('auth.login.submit') }}</AppButton>
        </NuxtLink>
      </template>
    </ConfirmEmailPanel>
    <template #fallback><SkeletonBlock :lines="3" /></template>
  </ClientOnly>
</template>
