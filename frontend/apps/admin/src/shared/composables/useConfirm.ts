import { defineStore } from 'pinia'
import { shallowRef } from 'vue'

export interface ConfirmRequest {
  title: string
  message: string
  confirmText?: string
  /** Red button, for what cannot be undone. */
  danger?: boolean
  /** Runs while the dialog stays open. If it throws, the dialog stays and shows the error. */
  action: () => Promise<unknown>
}

/**
 * Every dangerous action goes through this, so that none of them can skip the question. ConfirmHost
 * in the layout draws the dialog.
 */
export const useConfirm = defineStore('confirm', () => {
  const request = shallowRef<ConfirmRequest | null>(null)

  return {
    request,
    ask(next: ConfirmRequest): void {
      request.value = next
    },
    close(): void {
      request.value = null
    },
  }
})
