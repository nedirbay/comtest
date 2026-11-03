import { ApiError, ErrorCode } from '@comtest/api-client'
import { computed, provide, reactive, ref, type InjectionKey, type Ref } from 'vue'
import type { z } from 'zod'

export interface FormContext {
  errors: Ref<Record<string, string>>
}

export const FORM_CONTEXT: InjectionKey<FormContext> = Symbol('comtest-form')

export interface UseFormOptions<Schema extends z.ZodType> {
  schema: Schema
  initial: z.input<Schema>
  /** Turns a key from the schema into text. Schemas hold i18n keys, not sentences. */
  translate: (key: string) => string
  /** Translates errors the API gave no text for, such as a lost connection. */
  describeError: (error: unknown) => string
  submit: (values: z.output<Schema>) => Promise<void>
}

/**
 * The form state used everywhere: validates with zod before calling the server, puts the server's
 * own field errors under the right inputs, and ignores a second click while the first one runs.
 */
export function useForm<Schema extends z.ZodType>(options: UseFormOptions<Schema>) {
  const values = reactive(structuredClone(options.initial as object)) as z.input<Schema>
  const errors = ref<Record<string, string>>({})
  const formError = ref<string | null>(null)
  const lastError = ref<unknown>(null)
  const submitting = ref(false)

  provide(FORM_CONTEXT, { errors })

  function reportFieldErrors(fieldErrors: Readonly<Record<string, string[]>>): void {
    errors.value = Object.fromEntries(
      Object.entries(fieldErrors)
        .filter(([, messages]) => messages.length > 0)
        .map(([field, messages]) => [field, messages[0] as string]),
    )
  }

  async function handleSubmit(): Promise<void> {
    if (submitting.value) {
      return
    }

    errors.value = {}
    formError.value = null
    lastError.value = null

    const parsed = options.schema.safeParse(values)

    if (!parsed.success) {
      const found: Record<string, string> = {}

      for (const issue of parsed.error.issues) {
        const path = issue.path.join('.')

        found[path] ??= options.translate(issue.message)
      }

      errors.value = found

      return
    }

    submitting.value = true

    try {
      await options.submit(parsed.data)
    } catch (error) {
      lastError.value = error

      const fieldErrors = error instanceof ApiError ? error.fieldErrors : {}

      if (Object.keys(fieldErrors).length > 0) {
        reportFieldErrors(fieldErrors)
      }

      // Field messages sit next to their inputs and do not need repeating above the form.
      const isFieldLevel =
        error instanceof ApiError &&
        error.code === ErrorCode.Validation &&
        Object.keys(error.fieldErrors).length > 0

      if (!isFieldLevel) {
        formError.value = options.describeError(error)
      }
    } finally {
      submitting.value = false
    }
  }

  return {
    values,
    errors,
    formError,
    lastError,
    submitting,
    hasErrors: computed(() => Object.keys(errors.value).length > 0 || formError.value !== null),
    handleSubmit,
    reset(): void {
      Object.assign(values as object, structuredClone(options.initial as object))
      errors.value = {}
      formError.value = null
    },
  }
}
