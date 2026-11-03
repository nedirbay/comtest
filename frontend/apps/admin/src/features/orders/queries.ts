import type { Schema } from '@comtest/api-client'

export const ORDERS_KEY = ['admin', 'orders']

type OrderStatus = Schema<'OrderStatus'>

/** The same table as `Order.Transitions` on the server, which has the final word. */
const NEXT_STATUSES: Record<OrderStatus, OrderStatus[]> = {
  pending: ['confirmed', 'cancelled'],
  confirmed: ['packed', 'cancelled'],
  packed: ['shipped', 'cancelled'],
  shipped: ['delivered', 'returned'],
  delivered: ['returned'],
  cancelled: [],
  returned: [],
}

export function nextStatuses(current: string): OrderStatus[] {
  return NEXT_STATUSES[current as OrderStatus] ?? []
}

/** These undo a sale. The server asks for `orders.manage`, and the panel for a confirmation. */
export function isUndoingStatus(status: OrderStatus): boolean {
  return status === 'cancelled' || status === 'returned'
}
