import { formatDate, formatDateTime, formatMoney } from '@comtest/ui'
import { useI18n } from 'vue-i18n'

/** The shared formatters, in the language on screen. They only display what the API calculated. */
export function useFormat() {
  const { locale } = useI18n({ useScope: 'global' })

  return {
    money: (amount: number, currency: string) => formatMoney({ amount, currency }, locale.value),
    date: (value: string) => formatDate(value, locale.value),
    dateTime: (value: string) => formatDateTime(value, locale.value),
  }
}
