const DAY_MS = 86_400_000

/** Asia/Ashgabat has one offset all year, the same zone the panel's dates are shown in. */
const STORE_OFFSET_MS = 5 * 3_600_000

export interface DayRange {
  /** `dd.mm` in the store's time zone. */
  label: string
  /** The first and last moments of the day, as the API's `from` and `to` take them. */
  from: string
  to: string
}

/** The start of the store's day holding this moment, as a UTC time. */
function storeDayStart(moment: number): number {
  return Math.floor((moment + STORE_OFFSET_MS) / DAY_MS) * DAY_MS - STORE_OFFSET_MS
}

/** The last `count` days, oldest first, ending with today. */
export function lastDays(count: number, now: Date = new Date()): DayRange[] {
  const today = storeDayStart(now.getTime())

  return Array.from({ length: count }, (_, index) => {
    const start = today - (count - 1 - index) * DAY_MS
    const shifted = new Date(start + STORE_OFFSET_MS)
    const day = String(shifted.getUTCDate()).padStart(2, '0')
    const month = String(shifted.getUTCMonth() + 1).padStart(2, '0')

    return {
      label: `${day}.${month}`,
      from: new Date(start).toISOString(),
      to: new Date(start + DAY_MS - 1).toISOString(),
    }
  })
}

/** Turns a `YYYY-MM-DD` from a date picker into the matching moment of the store's day. */
export function dayBoundary(day: string, edge: 'start' | 'end'): string | undefined {
  const start = Date.parse(`${day}T00:00:00Z`)

  if (Number.isNaN(start)) {
    return undefined
  }

  const utcStart = start - STORE_OFFSET_MS

  return new Date(edge === 'start' ? utcStart : utcStart + DAY_MS - 1).toISOString()
}
