import { defineConfig, devices } from '@playwright/test'

const API_URL = 'http://localhost:5055'

/** The stack comes first: `e2e/stack.sh up`. It has ports of its own, away from a running dev setup. */
/** Set E2E_APPS to `admin` or `storefront` to start only that app's servers. */
const only = process.env.E2E_APPS
const channel = process.env.PLAYWRIGHT_CHANNEL ?? 'chrome'

export default defineConfig({
  testDir: './e2e',
  snapshotPathTemplate: '{testDir}/__screenshots__/{projectName}/{arg}{ext}',
  // Screenshots depend on the machine's font rendering, so they only compare on a known machine.
  testIgnore: process.env.VISUAL ? undefined : ['**/visual.spec.ts'],
  globalSetup: only === 'admin' ? undefined : './e2e/support/global-setup.ts',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: [['list']],
  outputDir: '.e2e/results',
  use: {
    // Turkmen is the default language, and the tests read the interface text.
    locale: 'tk-TM',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'], channel } },
    { name: 'phone', use: { ...devices['Pixel 7'], channel } },
    // Other engines on request (`E2E_BROWSERS=all`), as they take longer and need their own download.
    ...(process.env.E2E_BROWSERS === 'all'
      ? [
          { name: 'firefox', use: devices['Desktop Firefox'] },
          { name: 'webkit', use: devices['Desktop Safari'] },
        ]
      : []),
  ],
  // These only serve what `pnpm test:e2e` built first. Building here would rewrite the storefront's
  // files under the server that uses them.
  webServer: (
    [
      {
        app: 'storefront',
        command: 'node apps/storefront/.output/server/index.mjs',
        url: 'http://localhost:3001',
        reuseExistingServer: !process.env.CI,
        env: {
          PORT: '3001',
          NUXT_PUBLIC_API_URL: API_URL,
          NUXT_PUBLIC_DEV_TENANT: 'demo',
          // Blocks instead of reporting, so that any page the policy would break fails its tests.
          NUXT_CSP_MODE: 'enforce',
          NUXT_CSP_EXTRA_IMAGE_ORIGINS: 'http://localhost:59000',
        },
      },
      {
        app: 'storefront',
        // Set up as in production: without X-Tenant, the store comes from the host name.
        command: 'node apps/storefront/.output/server/index.mjs',
        url: 'http://localhost:3002',
        reuseExistingServer: !process.env.CI,
        env: {
          PORT: '3002',
          NUXT_PUBLIC_API_URL: '',
          NUXT_API_INTERNAL_URL: API_URL,
          NUXT_PUBLIC_DEV_TENANT: '',
        },
      },
      {
        app: 'storefront',
        // Pointed at a closed port, to see how the store behaves while the API is down.
        command: 'node apps/storefront/.output/server/index.mjs',
        url: 'http://localhost:3003',
        reuseExistingServer: !process.env.CI,
        env: {
          PORT: '3003',
          NUXT_PUBLIC_API_URL: 'http://localhost:9',
          NUXT_PUBLIC_DEV_TENANT: 'demo',
        },
      },
      {
        app: 'admin',
        command: 'pnpm --filter @comtest/admin exec vite preview --port 5174 --strictPort',
        url: 'http://localhost:5174',
        reuseExistingServer: !process.env.CI,
      },
    ] as const
  )
    .filter((server) => !only || server.app === only)
    .map(({ app: _app, ...server }) => server),
})
