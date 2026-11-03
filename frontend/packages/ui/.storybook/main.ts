import { elementPlusCssLayer } from '@comtest/config/vite'
import type { StorybookConfig } from '@storybook/vue3-vite'
import tailwindcss from '@tailwindcss/vite'
import vue from '@vitejs/plugin-vue'
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers'
import Components from 'unplugin-vue-components/vite'
import { mergeConfig } from 'vite'

const config: StorybookConfig = {
  stories: ['../src/stories/*.stories.ts'],
  framework: '@storybook/vue3-vite',
  core: { disableTelemetry: true },
  viteFinal: (base) =>
    mergeConfig(base, {
      plugins: [
        vue(),
        Components({
          dts: false,
          dirs: [],
          resolvers: [ElementPlusResolver({ importStyle: 'css' })],
        }),
        elementPlusCssLayer(),
        tailwindcss(),
      ],
    }),
}

export default config
