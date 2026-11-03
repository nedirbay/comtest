import { describe, expect, it, vi } from 'vitest'
import { ApiError, AuthSession, ErrorCode } from '../src'

const grant = (name: string) => ({
  accessToken: `header.${btoa(JSON.stringify({ sub: name }))}.signature`,
  expiresAt: new Date(Date.now() + 900_000).toISOString(),
})

const failure = (code: string, status = 401) => new ApiError({ status, code })

describe('AuthSession', () => {
  it('shares one refresh between callers that arrive at the same time', async () => {
    const refresh = vi.fn().mockResolvedValue(grant('user'))
    const session = new AuthSession({ refresh })

    const tokens = await Promise.all([session.refresh(), session.refresh(), session.refresh()])

    expect(refresh).toHaveBeenCalledTimes(1)
    expect(new Set(tokens).size).toBe(1)
  })

  it('renews a token that is about to expire before handing it out', async () => {
    const refresh = vi.fn().mockResolvedValue(grant('fresh'))
    const session = new AuthSession({ refresh })

    session.accept({ ...grant('old'), expiresAt: new Date(Date.now() + 5_000).toISOString() })

    expect(await session.getToken()).toBe(grant('fresh').accessToken)
    expect(refresh).toHaveBeenCalledOnce()
  })

  it('does not call the server for a guest', async () => {
    const refresh = vi.fn()
    const session = new AuthSession({ refresh })

    expect(await session.getToken()).toBeNull()
    expect(refresh).not.toHaveBeenCalled()
  })

  it('ends the session when the server rejects the refresh token', async () => {
    const onExpired = vi.fn()
    const session = new AuthSession({
      refresh: vi.fn().mockRejectedValue(failure(ErrorCode.RefreshInvalid)),
      onExpired,
    })

    session.accept(grant('user'))

    expect(await session.refresh()).toBeNull()
    expect(session.isAuthenticated).toBe(false)
    expect(onExpired).toHaveBeenCalledOnce()
  })

  it('keeps the session when the network fails', async () => {
    const onExpired = vi.fn()
    const session = new AuthSession({
      refresh: vi.fn().mockRejectedValue(ApiError.network(new TypeError('offline'))),
      onExpired,
    })

    session.accept(grant('user'))

    await expect(session.refresh()).rejects.toThrow(ApiError)
    expect(session.isAuthenticated).toBe(true)
    expect(onExpired).not.toHaveBeenCalled()
  })

  it('keeps the session when the server is failing', async () => {
    const session = new AuthSession({
      refresh: vi.fn().mockRejectedValue(failure('common.unexpected', 500)),
    })

    session.accept(grant('user'))

    await expect(session.refresh()).rejects.toThrow(ApiError)
    expect(session.isAuthenticated).toBe(true)
  })

  it('retries once after a rotation conflict, then gives up', async () => {
    const refresh = vi
      .fn()
      .mockRejectedValueOnce(failure(ErrorCode.RefreshConflict, 409))
      .mockResolvedValueOnce(grant('user'))
    const session = new AuthSession({ refresh, conflictRetryDelayMs: 0 })

    expect(await session.refresh()).toBe(grant('user').accessToken)
    expect(refresh).toHaveBeenCalledTimes(2)

    const conflicted = vi.fn().mockRejectedValue(failure(ErrorCode.RefreshConflict, 409))
    const other = new AuthSession({ refresh: conflicted, conflictRetryDelayMs: 0 })

    expect(await other.refresh()).toBeNull()
    expect(conflicted).toHaveBeenCalledTimes(2)
  })

  it('reads roles and permissions from the token', () => {
    const payload = {
      sub: 'u1',
      sid: 's1',
      role: 'Manager',
      permission: ['catalog.read', 'orders.read'],
      mfa: true,
      tid: 't1',
    }
    const session = new AuthSession({ refresh: vi.fn() })

    session.accept({
      accessToken: `h.${btoa(JSON.stringify(payload)).replaceAll('=', '')}.s`,
      expiresAt: new Date(Date.now() + 900_000).toISOString(),
    })

    expect(session.claims).toEqual({
      userId: 'u1',
      sessionId: 's1',
      roles: ['Manager'],
      permissions: ['catalog.read', 'orders.read'],
      mfa: true,
      tenantId: 't1',
    })
  })
})
