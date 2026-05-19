import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
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
