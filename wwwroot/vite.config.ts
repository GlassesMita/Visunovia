import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src')
    }
  },
  base: './',
  build: {
    outDir: '../www_build',
    emptyOutDir: true,
    rollupOptions: {
      output: {
        manualChunks: {
          'baklavajs': ['@baklavajs/core', '@baklavajs/engine', '@baklavajs/renderer-vue'],
          'vue-vendor': ['vue', 'vue-router', 'pinia', 'vue-i18n'],
        }
      }
    }
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:28478',
        changeOrigin: true
      }
    }
  }
})
