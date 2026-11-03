import { sentryPrivacyOptions } from '@comtest/ui/monitoring'
import * as Sentry from '@sentry/vue'
import { describe, expect, it } from 'vitest'

const jwt = 'eyJhbGciOiJFUzI1NiJ9.eyJzdWIiOiIxIn0.c2lnbmF0dXJl'

/** Runs the real SDK and looks at the bytes it would have sent. */
function startWithRecordingTransport() {
  const sent: string[] = []

  Sentry.init({
    ...sentryPrivacyOptions,
    dsn: 'https://public@sentry.invalid/1',
    defaultIntegrations: false,
    transport: () => ({
      send: (envelope) => {
        sent.push(JSON.stringify(envelope))

        return Promise.resolve({})
      },
      flush: () => Promise.resolve(true),
    }),
  })

  return sent
}

describe('what reaches the error tracker', () => {
  it('contains neither the user nor secrets from the error', async () => {
    const sent = startWithRecordingTransport()

    Sentry.setUser({ id: 'user-1', email: 'ayna@example.tm', username: 'Aýna' })
    Sentry.addBreadcrumb({
      category: 'fetch',
      data: { url: 'https://x.tm/reset-password#token=breadcrumb-secret' },
    })
    Sentry.setContext('form', { password: 'context-secret', phoneNumber: '+99365123456' })
    Sentry.captureException(new Error(`Failed for ayna@example.tm with ${jwt} and +993 65 123456`))
    await Sentry.flush()

    const report = sent.join('\n')

    expect(report).toContain('Failed for')
    expect(report).not.toContain('ayna@example.tm')
    expect(report).not.toContain('Aýna')
    expect(report).not.toContain('user-1')
    expect(report).not.toContain(jwt)
    expect(report).not.toContain('123456')
    expect(report).not.toContain('breadcrumb-secret')
    expect(report).not.toContain('context-secret')
  })
})
