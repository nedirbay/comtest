import { ApiError } from '@comtest/api-client'
import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { z } from 'zod'
import { useForm } from '../src/composables/useForm'

const schema = z.object({
  email: z.email('validation.email'),
  password: z.string().min(12, 'validation.passwordLength'),
})

function setup(submit: (values: z.output<typeof schema>) => Promise<void>) {
  let form!: ReturnType<typeof useForm<typeof schema>>

  mount(
    defineComponent({
      setup() {
        form = useForm({
          schema,
          initial: { email: '', password: '' },
          translate: (key) => `t:${key}`,
          describeError: (error) => (error instanceof Error ? error.message : 'unknown'),
          submit,
        })

        return () => h('div')
      },
    }),
  )

  return form
}

const valid = { email: 'a@b.tm', password: 'a-long-enough-password' }

describe('useForm', () => {
  it('stops invalid input before it reaches the server', async () => {
    const submit = vi.fn()
    const form = setup(submit)

    await form.handleSubmit()

    expect(submit).not.toHaveBeenCalled()
    expect(form.errors.value).toEqual({
      email: 't:validation.email',
      password: 't:validation.passwordLength',
    })
  })

  it('ignores a second submit while the first is running', async () => {
    let finish!: () => void
    const submit = vi.fn(() => new Promise<void>((resolve) => (finish = resolve)))
    const form = setup(submit)

    Object.assign(form.values, valid)

    const first = form.handleSubmit()
    await form.handleSubmit()
    finish()
    await first

    expect(submit).toHaveBeenCalledOnce()
    expect(form.submitting.value).toBe(false)
  })

  it('puts server validation errors under their fields', async () => {
    const form = setup(() =>
      Promise.reject(
        new ApiError({
          status: 400,
          code: 'common.validation',
          title: 'Invalid',
          fieldErrors: { email: ['Already taken'] },
        }),
      ),
    )

    Object.assign(form.values, valid)
    await form.handleSubmit()

    expect(form.errors.value).toEqual({ email: 'Already taken' })
    expect(form.formError.value).toBeNull()
  })

  it('shows other failures above the form', async () => {
    const form = setup(() =>
      Promise.reject(
        new ApiError({ status: 401, code: 'auth.invalid_credentials', title: 'Wrong login' }),
      ),
    )

    Object.assign(form.values, valid)
    await form.handleSubmit()

    expect(form.formError.value).toBe('Wrong login')
    expect(form.errors.value).toEqual({})
  })
})
