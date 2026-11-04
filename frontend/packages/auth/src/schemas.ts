import { z } from '@comtest/ui/zod'

const MIN_PASSWORD_LENGTH = 12
const MAX_PASSWORD_LENGTH = 256

const required = z.string().trim().min(1, 'validation.required')
const email = z.email('validation.email')
const password = z
  .string()
  .min(MIN_PASSWORD_LENGTH, 'validation.passwordLength')
  .max(MAX_PASSWORD_LENGTH, 'validation.tooLong')
const otp = z.string().regex(/^\d{6}$/, 'validation.otp')

export const loginSchema = z.object({
  login: required,
  password: z.string().min(1, 'validation.required'),
})

export const registerSchema = z.object({
  email,
  fullName: required.max(200, 'validation.tooLong'),
  password,
})

export const emailSchema = z.object({ email })

export const profileSchema = z.object({
  fullName: required.max(200, 'validation.tooLong'),
  locale: z.string(),
})

export const changeEmailSchema = z.object({
  newEmail: email,
  password: z.string().min(1, 'validation.required'),
})

export const resetPasswordSchema = z
  .object({ password, confirm: z.string() })
  .refine((value) => value.password === value.confirm, {
    path: ['confirm'],
    message: 'validation.passwordMatch',
  })

export const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, 'validation.required'),
    newPassword: password,
    confirm: z.string(),
  })
  .refine((value) => value.newPassword === value.confirm, {
    path: ['confirm'],
    message: 'validation.passwordMatch',
  })

export const otpSchema = z.object({ code: otp })

export const recoveryCodeSchema = z.object({ recoveryCode: required })

export const disableTwoFactorSchema = z.object({
  password: z.string().min(1, 'validation.required'),
  code: otp,
})
