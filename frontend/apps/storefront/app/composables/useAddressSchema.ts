import { normalizePhone } from '@comtest/ui'
import { z } from '@comtest/ui/zod'
import type { AddressValues } from '~/components/checkout/AddressFields.vue'

export const EMPTY_ADDRESS: AddressValues = {
  recipient: '',
  phone: '',
  region: '',
  district: '',
  city: '',
  line1: '',
  line2: '',
  postalCode: '',
}

const required = z.string().trim().min(1, 'validation.required')

export const phoneField = z
  .string()
  .transform((value) => normalizePhone(value) ?? '')
  .refine((value) => value !== '', 'validation.phone')

export const addressSchema = z.object({
  recipient: required.max(200, 'validation.tooLong'),
  phone: phoneField,
  region: required.max(100, 'validation.tooLong'),
  district: z.string().max(100, 'validation.tooLong'),
  city: z.string().max(100, 'validation.tooLong'),
  line1: required.max(300, 'validation.tooLong'),
  line2: z.string().max(300, 'validation.tooLong'),
  postalCode: z.string().max(20, 'validation.tooLong'),
})

/** The API takes absent optional fields as null, not as empty text. */
export function toAddressData(values: z.output<typeof addressSchema>) {
  return {
    ...values,
    district: values.district || null,
    city: values.city || null,
    line2: values.line2 || null,
    postalCode: values.postalCode || null,
  }
}
