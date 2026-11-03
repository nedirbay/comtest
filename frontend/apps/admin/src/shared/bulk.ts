export interface BulkResult {
  succeeded: number
  failed: number
  firstError: unknown
}

const CONCURRENT_REQUESTS = 4

/**
 * The API has no bulk endpoints, so each item is its own request. One failing item must not stop
 * the rest, and the user has to hear how many went through.
 */
export async function runBulk<Item>(
  items: readonly Item[],
  action: (item: Item) => Promise<unknown>,
): Promise<BulkResult> {
  const result: BulkResult = { succeeded: 0, failed: 0, firstError: null }
  const queue = [...items]

  async function worker(): Promise<void> {
    for (let item = queue.shift(); item !== undefined; item = queue.shift()) {
      try {
        await action(item)
        result.succeeded += 1
      } catch (error) {
        result.failed += 1
        result.firstError ??= error
      }
    }
  }

  await Promise.all(Array.from({ length: CONCURRENT_REQUESTS }, worker))

  return result
}
