const MAX_BYTES = 8 * 1024

/**
 * Collects what the browser reports the page's policy blocked. Anyone can post here, so the size is
 * limited, only known fields are logged, and addresses lose their query, where private data can sit.
 */
export default defineEventHandler(async (event) => {
  setResponseStatus(event, 204)

  const raw = await readRawBody(event, 'utf8')

  if (!raw || raw.length > MAX_BYTES) {
    return null
  }

  try {
    const report = (JSON.parse(raw) as { 'csp-report'?: Record<string, unknown> })['csp-report']
    const path = (value: unknown) =>
      typeof value === 'string' ? value.split(/[?#]/)[0] : undefined

    if (report) {
      console.warn(
        JSON.stringify({
          event: 'csp-violation',
          directive: report['effective-directive'] ?? report['violated-directive'],
          blocked: path(report['blocked-uri']),
          page: path(report['document-uri']),
        }),
      )
    }
  } catch {
    // Not a report, and nothing worth logging.
  }

  return null
})
