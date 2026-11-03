import { authMessages } from '@comtest/auth/i18n'
import { uiMessages } from '@comtest/ui/i18n'
import { storeMessages } from '../messages'

export default defineI18nLocale(() => ({
  ...uiMessages.ru,
  ...authMessages.ru,
  ...storeMessages.ru,
}))
