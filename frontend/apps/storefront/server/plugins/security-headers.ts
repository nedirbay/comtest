import { randomBytes } from 'node:crypto'

const SCRIPT_OR_STYLE = /<(script|style)(?=[\s>])/g

function originOf(url: string): string | undefined {
  try {
    return new URL(url).origin
  } catch {
    return undefined
  }
}

/**
 * The policy for a rendered page. Scripts need this response's nonce or to come from this site, so
 * text that slips into the page cannot run. Inline style attributes stay allowed because Element
 * Plus positions its popups with them, and a style attribute cannot run code.
 */
function buildPolicy(nonce: string, apiUrl: string, extraImages: string): string {
  const api = originOf(apiUrl)
  const connect = ["'self'", ...(api ? [api] : [])]
  const images = ["'self'", 'data:', 'blob:', 'https:', ...extraImages.split(/\s+/).filter(Boolean)]

  return [
    "default-src 'self'",
    `script-src 'self' 'nonce-${nonce}'`,
    `style-src 'self' 'nonce-${nonce}'`,
    "style-src-attr 'unsafe-inline'",
    `img-src ${images.join(' ')}`,
    // Vite inlines the smallest font subsets, and a font cannot run code.
    "font-src 'self' data:",
    `connect-src ${connect.join(' ')}`,
    "manifest-src 'self'",
    "worker-src 'self'",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "frame-ancestors 'none'",
    'report-uri /_csp-report',
  ].join('; ')
}

export default defineNitroPlugin((nitro) => {
  nitro.hooks.hook('render:html', (html, { event }) => {
    const { public: publicConfig, cspMode, cspExtraImageOrigins } = useRuntimeConfig(event)
    const nonce = randomBytes(16).toString('base64')
    const withNonce = (part: string) => part.replace(SCRIPT_OR_STYLE, `<$1 nonce="${nonce}"`)

    html.head = html.head.map(withNonce)
    html.bodyPrepend = html.bodyPrepend.map(withNonce)
    html.body = html.body.map(withNonce)
    html.bodyAppend = html.bodyAppend.map(withNonce)

    setResponseHeaders(event, {
      [cspMode === 'enforce' ? 'Content-Security-Policy' : 'Content-Security-Policy-Report-Only']:
        buildPolicy(nonce, publicConfig.apiUrl, cspExtraImageOrigins),
      'X-Content-Type-Options': 'nosniff',
      'X-Frame-Options': 'DENY',
      'Referrer-Policy': 'strict-origin-when-cross-origin',
      'Permissions-Policy': 'camera=(), microphone=(), geolocation=(), payment=()',
    })
  })
})
