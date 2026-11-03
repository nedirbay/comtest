const PRIVATE_PATHS = [
  '/account',
  '/cart',
  '/checkout',
  '/order-confirmed',
  '/track',
  '/accept-invitation',
  '/login',
  '/register',
  '/forgot-password',
  '/reset-password',
  '/confirm-email',
  '/search',
  '/compare',
]
const PREFIXES = ['', '/ru', '/en']
const DAY_SECONDS = 86_400

export default defineEventHandler((event) => {
  const origin = getRequestURL(event, { xForwardedHost: true }).origin

  setResponseHeaders(event, {
    'Content-Type': 'text/plain; charset=utf-8',
    'Cache-Control': `public, max-age=${DAY_SECONDS}`,
  })

  return [
    'User-agent: *',
    ...PREFIXES.flatMap((prefix) => PRIVATE_PATHS.map((path) => `Disallow: ${prefix}${path}`)),
    '',
    `Sitemap: ${origin}/sitemap.xml`,
    '',
  ].join('\n')
})
