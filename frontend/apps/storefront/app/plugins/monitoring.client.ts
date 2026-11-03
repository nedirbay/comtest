import { sentryPrivacyOptions } from '@comtest/ui/monitoring'

/** Loaded only when an address is configured, so development and unset builds carry none of it. */
export default defineNuxtPlugin(async (nuxtApp) => {
  const dsn = useRuntimeConfig().public.sentryDsn

  if (!dsn) {
    return
  }

  const Sentry = await import('@sentry/vue')

  Sentry.init({
    ...sentryPrivacyOptions,
    app: nuxtApp.vueApp,
    dsn,
    tracesSampleRate: 0.05,
  })
})
