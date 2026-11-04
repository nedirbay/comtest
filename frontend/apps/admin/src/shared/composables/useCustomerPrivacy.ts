import { useAuthStore } from '@comtest/auth'
import { computed } from 'vue'
import { Permission } from '@/app/permissions'

const MASK = '•••'

export function maskName(value: string): string {
  const first = value.trim().charAt(0)

  return first ? `${first}${MASK}` : MASK
}

/** Keeps the last two digits, enough to tell two people apart on a call and no more. */
export function maskPhone(value: string): string {
  return `${MASK} ${value.replace(/\D/g, '').slice(-2)}`.trim()
}

export function maskEmail(value: string): string {
  const [local = '', domain = ''] = value.split('@')

  return domain ? `${local.charAt(0)}${MASK}@${domain}` : MASK
}

/**
 * Personal data is shown in full only with `customers.read`. The API is the real gate: without the
 * permission it sends `A***`, `***33` and no email or street. This puts those in the panel's own
 * form of the mask, and hides the region and city too rather than show an address with a hole in it.
 */
export function useCustomerPrivacy() {
  const auth = useAuthStore()
  const canSee = computed(() => auth.can(Permission.CustomersRead))

  return {
    canSee,
    name: (value: string | null | undefined) =>
      canSee.value || !value ? (value ?? '') : maskName(value),
    phone: (value: string | null | undefined) =>
      canSee.value || !value ? (value ?? '') : maskPhone(value),
    email: (value: string | null | undefined) =>
      canSee.value || !value ? (value ?? '') : maskEmail(value),
    /** An address cannot be partly hidden in a useful way, so it goes away entirely. */
    address: (value: string | null | undefined) => (canSee.value ? (value ?? '') : MASK),
  }
}
