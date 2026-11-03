const TURKMEN_MONTHS = [
  'ýanwar',
  'fewral',
  'mart',
  'aprel',
  'maý',
  'iýun',
  'iýul',
  'awgust',
  'sentýabr',
  'oktýabr',
  'noýabr',
  'dekabr',
] as const

/** The stores work in Turkmenistan. A fixed zone also keeps server and browser output identical. */
export const DEFAULT_TIME_ZONE = 'Asia/Ashgabat'

interface DateParts {
  year: string
  month: string
  day: string
  hour: string
  minute: string
}

function partsOf(value: Date, timeZone: string): DateParts {
  const parts = new Intl.DateTimeFormat('en-GB', {
    timeZone,
    year: 'numeric',
    month: 'numeric',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(value)

  const get = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((part) => part.type === type)?.value ?? ''

  return {
    year: get('year'),
    month: get('month'),
    day: get('day'),
    hour: get('hour'),
    minute: get('minute'),
  }
}

export function formatDate(
  value: string | Date,
  locale: string,
  timeZone: string = DEFAULT_TIME_ZONE,
): string {
  const date = new Date(value)

  if (locale === 'tk') {
    const { day, month, year } = partsOf(date, timeZone)

    return `${Number(day)} ${TURKMEN_MONTHS[Number(month) - 1]} ${year}`
  }

  return new Intl.DateTimeFormat(locale === 'en' ? 'en-GB' : 'ru-RU', {
    timeZone,
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  }).format(date)
}

export function formatDateTime(
  value: string | Date,
  locale: string,
  timeZone: string = DEFAULT_TIME_ZONE,
): string {
  const { hour, minute } = partsOf(new Date(value), timeZone)

  return `${formatDate(value, locale, timeZone)}, ${hour}:${minute}`
}
