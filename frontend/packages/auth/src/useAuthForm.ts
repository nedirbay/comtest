import { useApiErrorMessage, useForm, type UseFormOptions } from '@comtest/ui'
import { useI18n } from 'vue-i18n'
import type { z } from 'zod'

/** `useForm` with the app's translations wired in, which every auth form needs. */
export function useAuthForm<Schema extends z.ZodType>(
  options: Omit<UseFormOptions<Schema>, 'translate' | 'describeError'>,
) {
  const { t } = useI18n({ useScope: 'global' })
  const describeError = useApiErrorMessage()

  return useForm({ ...options, translate: (key) => t(key), describeError })
}
