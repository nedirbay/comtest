import type { H3Event } from 'h3'
import { createFetchWithHost, visitorAddress } from '~~/app/utils/fetchWithHost'

/**
 * Reads public store data from a server route. Every store has its own host name, and that name
 * decides whose data the API returns.
 */
export async function storeApiGet<T>(event: H3Event, path: string): Promise<T> {
  const config = useRuntimeConfig(event)
  const { host } = getRequestURL(event, { xForwardedHost: true })
  const baseUrl = config.apiInternalUrl || config.public.apiUrl
  const { devTenant } = config.public
  const visitor = config.apiInternalUrl
    ? visitorAddress(
        getRequestHeader(event, 'x-forwarded-for'),
        event.node.req.socket.remoteAddress,
      )
    : undefined
  const send = devTenant ? globalThis.fetch.bind(globalThis) : createFetchWithHost(host, visitor)
  const response = await send(
    new Request(`${baseUrl}/api/v1${path}`, {
      headers: devTenant ? { 'X-Tenant': devTenant } : {},
    }),
  )

  if (!response.ok) {
    throw createError({ status: response.status === 404 ? 404 : 503 })
  }

  return (await response.json()) as T
}
