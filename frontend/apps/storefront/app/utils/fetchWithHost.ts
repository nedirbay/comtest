import { request as httpRequest, type IncomingMessage } from 'node:http'
import { request as httpsRequest } from 'node:https'

const TIMEOUT_MS = 10_000
const NO_BODY_STATUSES = new Set([101, 204, 205, 304])

export interface VisitorAddress {
  ip?: string
}

/**
 * Only the last entry of X-Forwarded-For is the reverse proxy's own. Anything before it came from
 * the visitor, who can write what they like there.
 */
export function visitorAddress(
  forwardedFor: string | null | undefined,
  peer: string | undefined,
): VisitorAddress {
  const ip = forwardedFor?.split(',').at(-1)?.trim() || peer

  return ip ? { ip } : {}
}

function toHeaders(incoming: IncomingMessage): Headers {
  const headers = new Headers()

  for (const [name, value] of Object.entries(incoming.headers)) {
    for (const item of [value].flat()) {
      if (item !== undefined) {
        headers.append(name, item)
      }
    }
  }

  return headers
}

/**
 * Server rendering only. The API tells stores apart by host name, so it has to see the host the
 * visitor used. `fetch` silently replaces a Host header, and building the URL from that host would
 * let a forged header point the server at any address. So the connection always goes to the
 * configured API, and only this one header changes.
 *
 * Over the internal address this server also stands in for the reverse proxy, so `visitor` is set
 * there: the API would otherwise redirect plain HTTP, and count every visitor as this one machine.
 * The API believes these headers only from the network named in ForwardedHeaders__KnownNetworks.
 */
export function createFetchWithHost(
  host: string,
  visitor?: VisitorAddress,
): typeof globalThis.fetch {
  return (async (input: Request) => {
    const url = new URL(input.url)
    const body = input.body ? Buffer.from(await input.arrayBuffer()) : undefined
    const send = url.protocol === 'https:' ? httpsRequest : httpRequest

    return new Promise<Response>((resolve, reject) => {
      const outgoing = send(
        url,
        {
          method: input.method,
          headers: {
            ...Object.fromEntries(input.headers),
            host,
            ...(visitor
              ? {
                  'x-forwarded-proto': 'https',
                  ...(visitor.ip ? { 'x-forwarded-for': visitor.ip } : {}),
                }
              : {}),
          },
          timeout: TIMEOUT_MS,
        },
        (incoming) => {
          const chunks: Buffer[] = []

          incoming.on('data', (chunk: Buffer) => chunks.push(chunk))
          incoming.on('error', reject)
          incoming.on('end', () => {
            const status = incoming.statusCode ?? 502

            resolve(
              new Response(NO_BODY_STATUSES.has(status) ? null : Buffer.concat(chunks), {
                status,
                headers: toHeaders(incoming),
              }),
            )
          })
        },
      )

      outgoing.on('timeout', () => outgoing.destroy(new Error('The API did not answer in time')))
      outgoing.on('error', reject)
      outgoing.end(body)
    })
  }) as typeof globalThis.fetch
}
