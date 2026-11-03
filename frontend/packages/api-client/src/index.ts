export { readAccessClaims, type AccessClaims } from './claims'
export { createApi, unwrap, type Api, type ApiClient, type ApiOptions } from './client'
export { ApiError, ErrorCode, isApiError, type ProblemBody } from './errors'
export type { components, operations, paths } from './generated/schema'
export { AuthSession, type AccessGrant, type AuthSessionOptions } from './session'

import type { components } from './generated/schema'

/** Shorthand for the API's named schemas: `Schema<'ProfileDto'>`. */
export type Schema<Name extends keyof components['schemas']> = components['schemas'][Name]
