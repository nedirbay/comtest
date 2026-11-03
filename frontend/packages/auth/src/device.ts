const BROWSERS: [RegExp, string][] = [
  [/Edg\//, 'Edge'],
  [/OPR\//, 'Opera'],
  [/YaBrowser\//, 'Yandex Browser'],
  [/Chrome\//, 'Chrome'],
  [/Firefox\//, 'Firefox'],
  [/Safari\//, 'Safari'],
]

const SYSTEMS: [RegExp, string][] = [
  [/Android/, 'Android'],
  [/iPhone|iPad/, 'iOS'],
  [/Windows/, 'Windows'],
  [/Mac OS X/, 'macOS'],
  [/Linux/, 'Linux'],
]

const match = (value: string, list: [RegExp, string][]) =>
  list.find(([pattern]) => pattern.test(value))?.[1]

/** A readable name for a session's browser. It is only a label, so an unusual agent stays as it is. */
export function describeDevice(userAgent: string | null | undefined): string {
  if (!userAgent) {
    return '—'
  }

  const browser = match(userAgent, BROWSERS)
  const system = match(userAgent, SYSTEMS)

  return browser && system ? `${browser}, ${system}` : userAgent.slice(0, 60)
}
