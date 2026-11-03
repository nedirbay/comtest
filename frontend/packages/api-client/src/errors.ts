/** Stable error codes the interface reacts to. The backend owns the full list. */
export const ErrorCode = {
  Validation: 'common.validation',
  Unauthorized: 'common.unauthorized',
  Forbidden: 'common.forbidden',
  NotFound: 'common.not_found',
  ConcurrencyConflict: 'common.concurrency_conflict',
  TooManyRequests: 'common.too_many_requests',
  InvalidCredentials: 'auth.invalid_credentials',
  EmailNotConfirmed: 'auth.email_not_confirmed',
  InvalidToken: 'auth.invalid_token',
  InvalidTwoFactorCode: 'auth.invalid_two_factor_code',
  RefreshInvalid: 'auth.refresh_invalid',
  RefreshConflict: 'auth.refresh_conflict',
  MfaRequired: 'auth.mfa_required',
  WeakPassword: 'auth.weak_password',
  TenantNotFound: 'tenant.not_found',
  TenantSuspended: 'tenant.suspended',
  TenantRequired: 'tenant.required',
  Network: 'client.network',
} as const

export interface ProblemBody {
  title?: string
  status?: number
  code?: string
  correlationId?: string
  errors?: Record<string, string[]>
}

/** A failed API call. `title` is already translated by the server for the request's language. */
export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly correlationId: string | undefined
  readonly fieldErrors: Readonly<Record<string, string[]>>
  readonly retryAfterSeconds: number | undefined

  constructor(init: {
    status: number
    code: string
    title?: string | undefined
    correlationId?: string | undefined
    fieldErrors?: Record<string, string[]> | undefined
    retryAfterSeconds?: number | undefined
    cause?: unknown
  }) {
    super(init.title ?? init.code, { cause: init.cause })
    this.name = 'ApiError'
    this.status = init.status
    this.code = init.code
    this.correlationId = init.correlationId
    this.fieldErrors = init.fieldErrors ?? {}
    this.retryAfterSeconds = init.retryAfterSeconds
  }

  /** True when the server never answered: offline, DNS, CORS or a dropped connection. */
  get isNetworkError(): boolean {
    return this.code === ErrorCode.Network
  }

  static fromResponse(response: Response, body: unknown): ApiError {
    const problem = isProblem(body) ? body : {}
    const retryAfter = Number(response.headers.get('Retry-After'))

    return new ApiError({
      status: response.status,
      code: problem.code ?? `http.${response.status}`,
      title: problem.title,
      correlationId: problem.correlationId ?? response.headers.get('X-Correlation-ID') ?? undefined,
      fieldErrors: problem.errors,
      retryAfterSeconds: Number.isFinite(retryAfter) && retryAfter > 0 ? retryAfter : undefined,
    })
  }

  static network(cause: unknown): ApiError {
    return new ApiError({ status: 0, code: ErrorCode.Network, cause })
  }
}

function isProblem(body: unknown): body is ProblemBody {
  return typeof body === 'object' && body !== null
}

export function isApiError(error: unknown, code?: string): error is ApiError {
  return error instanceof ApiError && (code === undefined || error.code === code)
}
