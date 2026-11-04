export const ORDER_STATUSES = [
  'pending',
  'confirmed',
  'packed',
  'shipped',
  'delivered',
  'cancelled',
  'returned',
] as const
export type OrderStatusName = (typeof ORDER_STATUSES)[number]

export const PAYMENT_STATES = ['unpaid', 'paid', 'partiallyRefunded', 'refunded'] as const

export type StatusKind =
  | 'orderStatus'
  | 'paymentState'
  | 'paymentMethod'
  | 'paymentStatus'
  | 'refundStatus'
  | 'shippingType'
  | 'productStatus'
  | 'tenantStatus'
  | 'mediaStatus'

type TagType = 'primary' | 'success' | 'info' | 'warning' | 'danger'

const TAG_TYPES: Partial<Record<string, TagType>> = {
  pending: 'warning',
  confirmed: 'primary',
  packed: 'primary',
  shipped: 'primary',
  delivered: 'success',
  cancelled: 'danger',
  returned: 'info',
  unpaid: 'warning',
  paid: 'success',
  partiallyRefunded: 'info',
  refunded: 'info',
  published: 'success',
  draft: 'info',
  archived: 'info',
  active: 'success',
  suspended: 'danger',
  ready: 'success',
  processing: 'warning',
  rejected: 'danger',
  approved: 'success',
}

export function statusTagType(value: string): TagType {
  return TAG_TYPES[value] ?? 'info'
}
