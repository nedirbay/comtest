import { CSS_LAYER_ORDER, elementPlusCssLayer } from '@comtest/config/vite'
import tailwindcss from '@tailwindcss/vite'

/** Personal pages are rendered in the browser only, so no user data can reach the SSR payload. */
const PRIVATE_PAGES = ['/account/**', '/ru/account/**', '/en/account/**']

/** Pages that are useless in search results. */
const NO_INDEX_PAGES = ['login', 'register', 'forgot-password', 'reset-password', 'confirm-email']

const prefixes = ['', '/ru', '/en']

export default defineNuxtConfig({
  compatibilityDate: '2026-10-01',
  devtools: { enabled: false },
  telemetry: false,
  typescript: { strict: true },

  modules: ['@element-plus/nuxt', '@pinia/nuxt', '@nuxtjs/i18n'],

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
      meta: [{ name: 'referrer', content: 'strict-origin-when-cross-origin' }],
      link: [{ rel: 'icon', type: 'image/svg+xml', href: '/favicon.svg' }],
    },
  },

  runtimeConfig: {
    // Server only, and never sent to the browser.
    apiInternalUrl: '',
    public: {
      apiUrl: 'http://localhost:5050',
      devTenant: '',
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

  vite: {
    plugins: [elementPlusCssLayer(), tailwindcss()],
    build: {
      // Maps are for uploading to the error tracker. They are not linked from the published files.
      sourcemap: 'hidden',
    },
  },
})
