export interface PlacedOrder {
  number: string
  token: string
  instructions: string | null
}

/**
 * What only the moment of ordering knows, such as bank details for a transfer. It stays in memory:
 * the proof that an order exists always comes from asking the server with the signed token.
 */
export const usePlacedOrder = () => useState<PlacedOrder | null>('placed-order', () => null)
