import { sentryPrivacyOptions } from '@comtest/ui/monitoring'
import type { App } from 'vue'
import type { Router } from 'vue-router'

/** Loaded only when an address is configured, so development and unset builds carry none of it. */
export async function startMonitoring(app: App, router: Router): Promise<void> {
  const dsn = import.meta.env.VITE_SENTRY_DSN

  if (!dsn) {
    return
  }

  const Sentry = await import('@sentry/vue')

  Sentry.init({
    ...sentryPrivacyOptions,
    app,
    dsn,
    environment: import.meta.env.MODE,
    integrations: [Sentry.browserTracingIntegration({ router })],
    tracesSampleRate: 0.05,
  })
}
