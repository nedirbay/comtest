const DAY_SECONDS = 86_400

/** Each store installs under its own name, so the manifest has to come from the request's host. */
export default defineEventHandler(async (event) => {
  const store = await storeApiGet<{ name: string }>(event, '/store').catch(() => ({
    name: 'ComTest',
  }))

  setResponseHeaders(event, {
    'Content-Type': 'application/manifest+json; charset=utf-8',
    'Cache-Control': `public, max-age=${DAY_SECONDS}`,
  })

  return {
    name: store.name,
    short_name: store.name,
    start_url: '/',
    scope: '/',
    display: 'standalone',
    background_color: '#faf9f7',
    theme_color: '#145b45',
    icons: [{ src: '/favicon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' }],
  }
})
