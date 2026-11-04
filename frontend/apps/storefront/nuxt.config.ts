import { CSS_LAYER_ORDER, elementPlusCssLayer } from '@comtest/config/vite'
import tailwindcss from '@tailwindcss/vite'

/** Personal pages are rendered in the browser only, so no user data can reach the SSR payload. */
const PRIVATE_PATHS = [
  '/account/**',
  '/cart',
  '/checkout',
  '/order-confirmed',
  '/track',
  '/accept-invitation',
]

/** Pages that are useless in search results. */
const NO_INDEX_PAGES = [
  'login',
  'register',
  'forgot-password',
  'reset-password',
  'confirm-email',
  'confirm-email-change',
  'compare',
]

const prefixes = ['', '/ru', '/en']
const PRIVATE_PAGES = prefixes.flatMap((prefix) => PRIVATE_PATHS.map((path) => prefix + path))

export default defineNuxtConfig({
  compatibilityDate: '2026-10-01',
  devtools: { enabled: false },
  telemetry: false,
  typescript: { strict: true },

  modules: ['@element-plus/nuxt', '@pinia/nuxt', '@nuxtjs/i18n'],

  components: [{ path: '~/components', pathPrefix: false }],

  css: ['@fontsource-variable/manrope/index.css', '~/assets/css/main.css'],

  elementPlus: { importStyle: 'css', icon: false },

  i18n: {
    strategy: 'prefix_except_default',
    defaultLocale: 'tk',
    langDir: 'locales',
    locales: [
      { code: 'tk', language: 'tk-TM', name: 'Türkmençe', file: 'tk.ts' },
      { code: 'ru', language: 'ru-RU', name: 'Русский', file: 'ru.ts' },
      { code: 'en', language: 'en-GB', name: 'English', file: 'en.ts' },
    ],
    detectBrowserLanguage: {
      useCookie: true,
      cookieKey: 'comtest-locale',
      redirectOn: 'root',
    },
  },

  app: {
    head: {
      style: [{ innerHTML: CSS_LAYER_ORDER, tagPriority: 'critical' }],
      meta: [
        { name: 'referrer', content: 'strict-origin-when-cross-origin' },
        { name: 'theme-color', content: '#145b45' },
      ],
      link: [
        { rel: 'icon', type: 'image/svg+xml', href: '/favicon.svg' },
        { rel: 'manifest', href: '/manifest.webmanifest' },
      ],
    },
  },

  runtimeConfig: {
    // Server only, and never sent to the browser.
    apiInternalUrl: '',
    // `report` only logs what the policy would block, and `enforce` blocks it. See server/plugins.
    cspMode: 'report',
    // Where product pictures come from when that is not https, as with local object storage.
    cspExtraImageOrigins: '',
    public: {
      apiUrl: 'http://localhost:5050',
      devTenant: '',
      adminUrl: 'http://localhost:5173',
      sentryDsn: '',
    },
  },

  routeRules: {
    ...Object.fromEntries(
      PRIVATE_PAGES.map((path) => [
        path,
        { ssr: false, headers: { 'X-Robots-Tag': 'noindex', 'Cache-Control': 'no-store' } },
      ]),
    ),
    ...Object.fromEntries(
      prefixes.flatMap((prefix) =>
        NO_INDEX_PAGES.map((page) => [
          `${prefix}/${page}`,
          { headers: { 'X-Robots-Tag': 'noindex' } },
        ]),
      ),
    ),
  },

  build: {
    transpile: ['@comtest/ui', '@comtest/auth', '@comtest/api-client'],
  },

  // None by default, so nothing can publish them by accident. See the task list's F15.
  sourcemap: { server: false, client: false },

  vite: {
    plugins: [elementPlusCssLayer(), tailwindcss()],
  },

  experimental: {
    defaults: {
      // Pages load their code when a visitor reaches for a link and not whenever one scrolls into
      // view, since a page full of product cards would otherwise download half the site.
      nuxtLink: { prefetch: true, prefetchOn: { interaction: true, visibility: false } },
    },
  },

  hooks: {
    // Same reason: the HTML should not ask the browser to fetch every page's code in advance.
    'build:manifest': (manifest) => {
      for (const entry of Object.values(manifest)) {
        entry.dynamicImports = []
        entry.prefetch = false
      }
    },
  },

  // Ready-made gzip and brotli copies of static files. On a slow connection this matters most.
  nitro: { compressPublicAssets: true },
})
