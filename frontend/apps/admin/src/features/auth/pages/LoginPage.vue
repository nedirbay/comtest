<script setup lang="ts">
import { LoginForm, safeReturnUrl } from '@comtest/auth'
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink, useRoute, useRouter } from 'vue-router'

const { t, locale } = useI18n({ useScope: 'global' })
const route = useRoute()
const router = useRouter()
const stage = ref<'password' | 'two-factor'>('password')

function onSignedIn(): void {
  void router.replace(safeReturnUrl(route.query.returnUrl, `/${locale.value}`))
}
</script>

<template>
  <h1 class="mb-6 text-2xl font-bold tracking-tight text-ink">
    {{ stage === 'password' ? t('auth.login.title') : t('auth.login.twoFactorTitle') }}
  </h1>
  <LoginForm @signed-in="onSignedIn" @stage-change="stage = $event">
    <template #forgot>
      <RouterLink
        :to="{ name: 'forgot-password' }"
        class="font-semibold text-primary hover:underline"
      >
        {{ t('auth.login.forgot') }}
      </RouterLink>
    </template>
  </LoginForm>
</template>
