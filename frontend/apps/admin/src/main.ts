import './styles/main.css'
import '@fontsource-variable/manrope/index.css'

import { createApi } from '@comtest/api-client'
import { API_KEY, useAuthStore, vCan } from '@comtest/auth'
import { VueQueryPlugin } from '@tanstack/vue-query'
import { createPinia } from 'pinia'
import { createApp } from 'vue'
import App from './App.vue'
import { i18n, preferredLocale } from './app/i18n'
import { startMonitoring } from './app/monitoring'
import { queryClient } from './app/query'
import { createAppRouter } from './app/router'
import './app/theme'

const app = createApp(App)
const pinia = createPinia()

const api = createApi({
  baseUrl: import.meta.env.VITE_API_URL,
  getLocale: () => i18n.global.locale.value,
  locks: navigator.locks ?? null,
  onSessionExpired: () => useAuthStore(pinia).handleExpired(),
})

const router = createAppRouter(pinia)

app.provide(API_KEY, api)
app.use(pinia)
app.use(i18n)
app.use(VueQueryPlugin, { queryClient })
app.use(router)
app.directive('can', vCan)

app.config.errorHandler = (error) => {
  console.error(error)
}

// Whatever ends the session, in this tab or another, nothing of the user's may stay behind.
useAuthStore(pinia).onSignedOut(() => {
  queryClient.clear()

  const current = router.currentRoute.value

  if (!current.meta.public) {
    void router.replace({
      name: 'login',
      params: { locale: current.params.locale ?? preferredLocale() },
    })
  }
})

void startMonitoring(app, router)

app.mount('#app')
