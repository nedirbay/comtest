import { describe, expect, it } from 'vitest'
import { dayBoundary, lastDays } from '../src/features/dashboard/days'

describe('store days', () => {
  it('cuts days at midnight in Ashgabat, not in UTC', () => {
    // 20:30 UTC is already the next day in Ashgabat, five hours ahead.
    const days = lastDays(2, new Date('2026-11-03T20:30:00Z'))

    expect(days[1]).toEqual({
      label: '04.11',
      from: '2026-11-03T19:00:00.000Z',
      to: '2026-11-04T18:59:59.999Z',
    })
    expect(days[0]?.label).toBe('03.11')
  })

  it('gives days that touch, with no gap and no overlap', () => {
    const days = lastDays(14, new Date('2026-03-01T01:00:00Z'))

    expect(days).toHaveLength(14)

    for (let index = 1; index < days.length; index += 1) {
      const previousEnd = new Date(days[index - 1]!.to).getTime()

      expect(new Date(days[index]!.from).getTime()).toBe(previousEnd + 1)
    }
  })

  it('crosses a month boundary', () => {
    const days = lastDays(3, new Date('2026-03-01T12:00:00Z'))

    expect(days.map((day) => day.label)).toEqual(['27.02', '28.02', '01.03'])
  })

  it('turns a picked date into the edges of that store day', () => {
    expect(dayBoundary('2026-11-03', 'start')).toBe('2026-11-02T19:00:00.000Z')
    expect(dayBoundary('2026-11-03', 'end')).toBe('2026-11-03T18:59:59.999Z')
    expect(dayBoundary('not-a-date', 'start')).toBeUndefined()
  })
})
