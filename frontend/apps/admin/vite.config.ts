import { adminContentSecurityPolicy, elementPlusCssLayer } from '@comtest/config/vite'
import tailwindcss from '@tailwindcss/vite'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath } from 'node:url'
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers'
import Components from 'unplugin-vue-components/vite'
import { defineConfig, loadEnv } from 'vite'

export default defineConfig(({ mode }) => {
  const env = { ...loadEnv(mode, process.cwd(), 'VITE_'), ...process.env }

  return {
    plugins: [
      vue(),
      Components({
        dts: false,
        dirs: [],
        resolvers: [ElementPlusResolver({ importStyle: 'css' })],
      }),
      elementPlusCssLayer(),
      tailwindcss(),
      adminContentSecurityPolicy({
        apiUrl: env.VITE_API_URL,
        extraOrigins: env.VITE_CSP_EXTRA_ORIGINS,
      }),
    ],
    resolve: {
      alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
    },
    server: { port: 5173, strictPort: true },
    build: {
      // None by default, so nothing can publish them by accident.
      sourcemap: false,
      chunkSizeWarningLimit: 700,
    },
  }
})
