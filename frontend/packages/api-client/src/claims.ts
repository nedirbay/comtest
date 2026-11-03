export interface AccessClaims {
  userId: string
  sessionId: string
  roles: string[]
  permissions: string[]
  mfa: boolean
  tenantId: string | null
}

/**
 * Reads what the access token says about its owner, to decide what to show. The signature is not
 * checked here and does not need to be: every decision that matters is taken again by the server.
 */
export function readAccessClaims(token: string): AccessClaims | null {
  const payload = token.split('.')[1]

  if (!payload) {
    return null
  }

  try {
    const claims = JSON.parse(decodeBase64Url(payload)) as Record<string, unknown>

    return {
      userId: String(claims.sub ?? ''),
      sessionId: String(claims.sid ?? ''),
      roles: toList(claims.role),
      permissions: toList(claims.permission),
      mfa: claims.mfa === true || claims.mfa === 'true',
      tenantId: typeof claims.tid === 'string' ? claims.tid : null,
    }
  } catch {
    return null
  }
}

function toList(value: unknown): string[] {
  if (Array.isArray(value)) {
    return value.map(String)
  }

  return typeof value === 'string' ? [value] : []
}

function decodeBase64Url(value: string): string {
  const base64 = value.replaceAll('-', '+').replaceAll('_', '/')
  const bytes = Uint8Array.from(atob(base64), (char) => char.charCodeAt(0))

  return new TextDecoder().decode(bytes)
}
