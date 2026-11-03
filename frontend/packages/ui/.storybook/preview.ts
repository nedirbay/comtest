import './preview.css'
import '@fontsource-variable/manrope/index.css'

import type { Preview } from '@storybook/vue3-vite'
import { setup } from '@storybook/vue3-vite'
import { watchEffect } from 'vue'
import { createI18n } from 'vue-i18n'
import { uiMessages } from '../src/i18n'
import type { AppLocale } from '../src/locale'

const i18n = createI18n({ legacy: false, locale: 'tk', fallbackLocale: 'en', messages: uiMessages })

setup((app) => {
  app.use(i18n)
})

const preview: Preview = {
  globalTypes: {
    locale: {
      description: 'Interface language',
      toolbar: {
        title: 'Dil',
        icon: 'globe',
        items: [
          { value: 'tk', title: 'Türkmençe' },
          { value: 'ru', title: 'Русский' },
          { value: 'en', title: 'English' },
        ],
        dynamicTitle: true,
      },
    },
    theme: {
      description: 'Colour theme',
      toolbar: {
        title: 'Tema',
        icon: 'contrast',
        items: [
          { value: 'light', title: 'Light' },
          { value: 'dark', title: 'Dark' },
        ],
        dynamicTitle: true,
      },
    },
  },
  initialGlobals: { locale: 'tk', theme: 'light' },
  decorators: [
    (story, context) => {
      watchEffect(() => {
        i18n.global.locale.value = context.globals.locale as AppLocale
        document.documentElement.classList.toggle('dark', context.globals.theme === 'dark')
      })

      return {
        components: { story },
        template: '<div class="bg-canvas p-6 text-ink"><story /></div>',
      }
    },
  ],
}

export default preview
