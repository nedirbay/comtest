import { defineConfig, devices } from '@playwright/test'

const API_URL = 'http://localhost:5055'

/** The stack comes first: `e2e/stack.sh up`. It has ports of its own, away from a running dev setup. */
export default defineConfig({
  testDir: './e2e',
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
    channel: process.env.PLAYWRIGHT_CHANNEL ?? 'chrome',
  },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'] } },
    { name: 'phone', use: { ...devices['Pixel 7'] } },
  ],
  // These only serve what `pnpm test:e2e` built first. Building here would rewrite the storefront's
  // files under the server that uses them.
  webServer: [
    {
      command: 'node apps/storefront/.output/server/index.mjs',
      url: 'http://localhost:3001',
      reuseExistingServer: !process.env.CI,
      env: {
        PORT: '3001',
        NUXT_PUBLIC_API_URL: API_URL,
        NUXT_PUBLIC_DEV_TENANT: 'demo',
      },
    },
    {
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
      command: 'pnpm --filter @comtest/admin exec vite preview --port 5174 --strictPort',
      url: 'http://localhost:5174',
      reuseExistingServer: !process.env.CI,
    },
  ],
})
