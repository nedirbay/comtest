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
