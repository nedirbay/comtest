import { readAccessClaims, type AccessClaims } from './claims'
import { ApiError, ErrorCode } from './errors'

export interface AccessGrant {
  accessToken: string
  expiresAt: string
}

export interface AuthSessionOptions {
  /** Exchanges the refresh cookie for a new access token. */
  refresh: () => Promise<AccessGrant>
  /** Called when the session really ended, and not when the network merely failed. */
  onExpired?: () => void
  now?: () => number
  /** Serializes refreshes across tabs. Rotation would otherwise make them collide. */
  locks?: Pick<LockManager, 'request'> | null
  conflictRetryDelayMs?: number
}

const EXPIRY_LEEWAY_MS = 30_000
const REFRESH_LOCK = 'comtest-auth-refresh'
const DEFAULT_CONFLICT_RETRY_DELAY_MS = 500

/**
 * Holds the access token in this object's private field and nowhere else. It is never written to
 * localStorage, sessionStorage or a readable cookie, so a page reload starts with a silent refresh.
 */
export class AuthSession {
  #token: string | null = null
  #expiresAt = 0
  #claims: AccessClaims | null = null
  #refreshing: Promise<string | null> | null = null
  readonly #options: AuthSessionOptions
  readonly #now: () => number
  readonly #listeners = new Set<() => void>()

  constructor(options: AuthSessionOptions) {
    this.#options = options
    this.#now = options.now ?? Date.now
  }

  get isAuthenticated(): boolean {
    return this.#token !== null
  }

  get claims(): AccessClaims | null {
    return this.#claims
  }

  /** Listens for any change of the signed-in state or the token's claims. */
  subscribe(listener: () => void): () => void {
    this.#listeners.add(listener)

    return () => this.#listeners.delete(listener)
  }

  accept(grant: AccessGrant): void {
    this.#token = grant.accessToken
    this.#expiresAt = new Date(grant.expiresAt).getTime()
    this.#claims = readAccessClaims(grant.accessToken)
    this.#notify()
  }

  clear(): void {
    this.#token = null
    this.#expiresAt = 0
    this.#claims = null
    this.#notify()
  }

  /** The current token, renewed first if it is about to expire. Null for a guest. */
  async getToken(): Promise<string | null> {
    if (this.#token === null) {
      return this.#refreshing
    }

    return this.#expiresAt - this.#now() > EXPIRY_LEEWAY_MS ? this.#token : this.refresh()
  }

  /**
   * Renews the token. Callers that arrive while a renewal is running share its result, because
   * the refresh token rotates: a second parallel request would look like a stolen token.
   */
  refresh(): Promise<string | null> {
    this.#refreshing ??= this.#refreshAcrossTabs().finally(() => {
      this.#refreshing = null
    })

    return this.#refreshing
  }

  async #refreshAcrossTabs(): Promise<string | null> {
    const locks = this.#options.locks

    return locks
      ? await locks.request(REFRESH_LOCK, () => this.#refreshOnce(true))
      : this.#refreshOnce(true)
  }

  async #refreshOnce(mayRetry: boolean): Promise<string | null> {
    try {
      this.accept(await this.#options.refresh())

      return this.#token
    } catch (error) {
      // Another tab or request is rotating right now, and the cookie is about to change.
      if (mayRetry && error instanceof ApiError && error.code === ErrorCode.RefreshConflict) {
        await delay(this.#options.conflictRetryDelayMs ?? DEFAULT_CONFLICT_RETRY_DELAY_MS)

        return this.#refreshOnce(false)
      }

      // Being offline says nothing about the session, so it must not sign the user out.
      if (error instanceof ApiError && !error.isNetworkError && error.status < 500) {
        this.clear()
        this.#options.onExpired?.()

        return null
      }

      throw error
    }
  }

  #notify(): void {
    for (const listener of this.#listeners) {
      listener()
    }
  }
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}
