<script setup lang="ts">
import { RegisterForm } from '@comtest/auth'
import { SkeletonBlock } from '@comtest/ui'

definePageMeta({ layout: 'auth' })

// The form appears with the JavaScript. Typed into any earlier, its input would not reach Vue,
// which on a slow connection means a filled-in field that fails as empty.

const { t } = useI18n()
const localePath = useLocalePath()

useHead({ title: computed(() => t('auth.register.title')) })
</script>

<template>
  <h1 class="mb-6 text-2xl font-bold tracking-tight text-ink">{{ t('auth.register.title') }}</h1>
  <ClientOnly>
    <RegisterForm />
    <template #fallback><SkeletonBlock :lines="7" /></template>
  </ClientOnly>
  <p class="mt-5 text-center text-sm text-ink-muted">
    {{ t('auth.register.haveAccount') }}
    <NuxtLink :to="localePath('/login')" class="font-semibold text-primary hover:underline">
      {{ t('auth.login.submit') }}
    </NuxtLink>
  </p>
</template>
