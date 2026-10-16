import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  // Cesium 的 Workers / Assets 等由 scripts/copy-cesium.mjs 複製到 public/cesiumStatic
  define: {
    CESIUM_BASE_URL: JSON.stringify('/cesiumStatic'),
  },
  plugins: [vue(), tailwindcss()],
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  server: {
    // Stage 4 起，/api 轉給後端
    proxy: { '/api': 'http://localhost:5000' },
  },
})
