import { createHash } from 'node:crypto'

/**
 * The order of the cascade layers decides who wins: Tailwind's reset cannot break Element Plus, and
 * a utility class can always override a component. A browser fixes the order the first time it sees
 * each layer, and the order stylesheets load in is up to the bundler. So this statement goes inline
 * at the very top of the page, ahead of every stylesheet.
 */
export const CSS_LAYER_ORDER =
  '@layer theme, base, element-plus, element-bridge, components, utilities;'

const elementPlusStyle = /[\\/]element-plus[\\/].*\.css(\?.*)?$/
const charset = /^\s*@charset\s+[^;]+;/

/**
 * Puts Element Plus's on-demand CSS into a cascade layer. Unlayered CSS beats every layer, so
 * without this a Tailwind utility could never override a component's own style. Pages built from
 * an index.html also get the layer order. Nuxt has none, so nuxt.config.ts adds it to the head.
 *
 * @returns {import('vite').Plugin}
 */
export function elementPlusCssLayer(layer = 'element-plus') {
  return {
    name: 'comtest:element-plus-css-layer',
    enforce: 'pre',
    transformIndexHtml() {
      return [{ tag: 'style', children: CSS_LAYER_ORDER, injectTo: 'head-prepend' }]
    },
    transform(code, id) {
      if (!elementPlusStyle.test(id)) {
        return null
      }

      // @charset is only allowed at the very start of a file, so it cannot stay inside the layer.
      return { code: `@layer ${layer} {\n${code.replace(charset, '')}\n}`, map: null }
    },
  }
}

/**
 * The admin panel's content security policy, as a meta tag in the built page, so that it holds on
 * any static host. Scripts can only come from this site. Inline style attributes stay allowed
 * because Element Plus positions its popups with them, and a style attribute cannot run code.
 *
 * A meta tag cannot set `frame-ancestors`: the web server has to send that one as a header.
 *
 * @param {{ apiUrl?: string, extraOrigins?: string }} options
 * @returns {import('vite').Plugin}
 */
export function adminContentSecurityPolicy({ apiUrl = '', extraOrigins = '' }) {
  const extras = extraOrigins.split(/\s+/).filter(Boolean)
  const api = apiUrl ? [new URL(apiUrl).origin] : []
  const layerHash = createHash('sha256').update(CSS_LAYER_ORDER).digest('base64')
  const policy = [
    "default-src 'self'",
    "script-src 'self'",
    `style-src 'self' 'sha256-${layerHash}'`,
    "style-src-attr 'unsafe-inline'",
    `img-src ${["'self'", 'data:', 'blob:', 'https:', ...extras].join(' ')}`,
    "font-src 'self' data:",
    // Named hosts only, so that a script slipped into the page cannot send data anywhere it likes.
    // Pictures go straight to object storage with a signed address, so its host is one of them.
    `connect-src ${["'self'", ...api, ...extras].join(' ')}`,
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
  ].join('; ')

  return {
    name: 'comtest:admin-csp',
    apply: 'build',
    transformIndexHtml: {
      order: 'post',
      handler() {
        return [
          {
            tag: 'meta',
            attrs: { 'http-equiv': 'Content-Security-Policy', content: policy },
            injectTo: 'head-prepend',
          },
        ]
      },
    },
  }
}
