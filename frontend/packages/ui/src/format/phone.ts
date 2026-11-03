/** Groups a Turkmen number for reading: +993 65 123456. Anything else is returned as it came. */
export function formatPhone(value: string): string {
  const digits = value.replace(/\D/g, '')

  if (digits.length === 11 && digits.startsWith('993')) {
    return `+993 ${digits.slice(3, 5)} ${digits.slice(5)}`
  }

  return value
}

/** The E.164 form the API stores, or null when the input is not a Turkmen number. */
export function normalizePhone(value: string): string | null {
  const digits = value.replace(/\D/g, '')

  if (digits.length === 8) {
    return `+993${digits}`
  }

  return digits.length === 11 && digits.startsWith('993') ? `+${digits}` : null
}
