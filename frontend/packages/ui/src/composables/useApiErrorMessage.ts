import { ApiError } from '@comtest/api-client'
import { useI18n } from 'vue-i18n'

/** The sentence to show for an error. The server's text wins, as it is already translated. */
export function useApiErrorMessage() {
  const { t } = useI18n({ useScope: 'global' })

  return function describe(error: unknown): string {
    if (error instanceof ApiError) {
      if (error.isNetworkError) {
        return t('errors.network')
      }

      if (error.retryAfterSeconds) {
        return t('errors.retryAfter', { seconds: error.retryAfterSeconds })
      }

      if (error.message !== error.code) {
        return error.message
      }
    }

    return t('errors.unexpected')
  }
}
