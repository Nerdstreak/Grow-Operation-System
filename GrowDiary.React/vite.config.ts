import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  // Relative asset URLs so the built app resolves assets against the runtime
  // <base href> the backend injects — required for the Home Assistant ingress,
  // where the app is served under a dynamic /api/hassio_ingress/<token>/ path.
  base: './',
  plugins: [react()],
  server: {
    host: '0.0.0.0',
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': {
        target: process.env.GROW_OS_BACKEND_URL ?? 'http://127.0.0.1:5076',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: '../GrowDiary.Web/wwwroot',
    emptyOutDir: false,
  },
})
