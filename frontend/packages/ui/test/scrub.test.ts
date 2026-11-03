import { describe, expect, it } from 'vitest'
import { REDACTED, scrubEvent, scrubUrl, scrubValue } from '../src/monitoring/scrub'

const jwt = 'eyJhbGciOiJFUzI1NiJ9.eyJzdWIiOiIxIn0.c2lnbmF0dXJl'

describe('scrubUrl', () => {
  it('drops the fragment and query, where email links carry their tokens', () => {
    expect(scrubUrl('https://x.tm/reset-password#userId=1&token=secret')).toBe(
      'https://x.tm/reset-password',
    )
    expect(scrubUrl('https://x.tm/a?token=secret&email=a@b.tm')).toBe('https://x.tm/a')
  })
})

describe('scrubValue', () => {
  it('redacts sensitive fields by name, however they are cased', () => {
    expect(
      scrubValue({ Password: 'x', accessToken: 'x', 'X-Authorization': 'x', fullName: 'x', ok: 1 }),
    ).toEqual({
      Password: REDACTED,
      accessToken: REDACTED,
      'X-Authorization': REDACTED,
      fullName: REDACTED,
      ok: 1,
    })
  })

  it('removes tokens, emails and phone numbers from free text', () => {
    const scrubbed = scrubValue(`failed for ayna@example.tm +993 65 123456 with ${jwt}`) as string

    expect(scrubbed).not.toContain('ayna@example.tm')
    expect(scrubbed).not.toContain('123456')
    expect(scrubbed).not.toContain(jwt)
  })

  it('survives circular references', () => {
    const value: Record<string, unknown> = { name: 'a' }
    value.self = value

    expect(() => scrubValue(value)).not.toThrow()
  })
})

describe('scrubEvent', () => {
  it('removes the user and the request content', () => {
    const event = scrubEvent({
      user: { id: '1', email: 'a@b.tm' },
      message: `boom ${jwt}`,
      request: {
        url: 'https://x.tm/reset-password#token=secret',
        headers: { Authorization: `Bearer ${jwt}` },
        cookies: { refresh: 'x' },
        data: { password: 'x' },
      },
    })

    expect(event.user).toBeUndefined()
    expect(event.message).toBe(`boom ${REDACTED}`)
    expect(event.request).toEqual({ url: 'https://x.tm/reset-password' })
  })
})
