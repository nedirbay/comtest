import { elementPlusCssLayer } from '@comtest/config/vite'
import tailwindcss from '@tailwindcss/vite'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath } from 'node:url'
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers'
import Components from 'unplugin-vue-components/vite'
import { defineConfig } from 'vite'

export default defineConfig({
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
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: { port: 5173, strictPort: true },
  build: {
    // Maps are for uploading to the error tracker. They are not linked from the published files.
    sourcemap: 'hidden',
    chunkSizeWarningLimit: 700,
  },
})
