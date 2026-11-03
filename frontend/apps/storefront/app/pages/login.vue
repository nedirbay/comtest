<script setup lang="ts">
import { LoginForm, safeReturnUrl, useAuthStore } from '@comtest/auth'
import { SkeletonBlock } from '@comtest/ui'

definePageMeta({ layout: 'auth' })

// The form appears with the JavaScript. Typed into any earlier, its input would not reach Vue,
// which on a slow connection means a filled-in field that fails as empty.

const { t } = useI18n()
const localePath = useLocalePath()
const route = useRoute()
const auth = useAuthStore()
const stage = ref<'password' | 'two-factor'>('password')

const title = computed(() =>
  stage.value === 'password' ? t('auth.login.title') : t('auth.login.twoFactorTitle'),
)

useHead({ title })

function goOnward(): void {
  void navigateTo(safeReturnUrl(route.query.returnUrl, localePath('/account')), { replace: true })
}

// Someone already signed in has no business here.
onMounted(async () => {
  await auth.bootstrap()

  if (auth.isAuthenticated) {
    goOnward()
  }
})
</script>

<template>
  <h1 class="mb-6 text-2xl font-bold tracking-tight text-ink">{{ title }}</h1>
  <ClientOnly>
    <LoginForm @signed-in="goOnward" @stage-change="stage = $event">
      <template #forgot>
        <NuxtLink
          :to="localePath('/forgot-password')"
          class="font-semibold text-primary hover:underline"
        >
          {{ t('auth.login.forgot') }}
        </NuxtLink>
      </template>
    </LoginForm>
    <template #fallback><SkeletonBlock :lines="6" /></template>
  </ClientOnly>
  <p v-if="stage === 'password'" class="mt-5 text-center text-sm text-ink-muted">
    {{ t('auth.login.noAccount') }}
    <NuxtLink :to="localePath('/register')" class="font-semibold text-primary hover:underline">
      {{ t('auth.register.submit') }}
    </NuxtLink>
  </p>
</template>
