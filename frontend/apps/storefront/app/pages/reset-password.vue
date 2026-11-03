<script setup lang="ts">
import { ResetPasswordForm } from '@comtest/auth'
import { AppButton, SkeletonBlock } from '@comtest/ui'

definePageMeta({ layout: 'auth' })

const { t } = useI18n()
const localePath = useLocalePath()

useHead({ title: computed(() => t('auth.reset.title')) })
</script>

<template>
  <h1 class="mb-6 text-2xl font-bold tracking-tight text-ink">{{ t('auth.reset.title') }}</h1>
  <ClientOnly>
    <ResetPasswordForm>
      <template #restart>
        <NuxtLink :to="localePath('/forgot-password')">
          <AppButton variant="primary">{{ t('auth.reset.requestNew') }}</AppButton>
        </NuxtLink>
      </template>
      <template #login>
        <NuxtLink :to="localePath('/login')">
          <AppButton variant="primary">{{ t('auth.login.submit') }}</AppButton>
        </NuxtLink>
      </template>
    </ResetPasswordForm>
    <template #fallback><SkeletonBlock :lines="5" /></template>
  </ClientOnly>
</template>
