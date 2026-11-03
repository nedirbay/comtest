<script setup lang="ts">
import { ForgotPasswordForm } from '@comtest/auth'
import { SkeletonBlock } from '@comtest/ui'

definePageMeta({ layout: 'auth' })

// The form appears with the JavaScript. Typed into any earlier, its input would not reach Vue,
// which on a slow connection means a filled-in field that fails as empty.

const { t } = useI18n()
const localePath = useLocalePath()

useHead({ title: computed(() => t('auth.forgot.title')) })
</script>

<template>
  <h1 class="mb-3 text-2xl font-bold tracking-tight text-ink">{{ t('auth.forgot.title') }}</h1>
  <ClientOnly>
    <ForgotPasswordForm />
    <template #fallback><SkeletonBlock :lines="3" /></template>
  </ClientOnly>
  <p class="mt-5 text-center text-sm">
    <NuxtLink :to="localePath('/login')" class="font-semibold text-primary hover:underline">
      {{ t('auth.forgot.backToLogin') }}
    </NuxtLink>
  </p>
</template>
