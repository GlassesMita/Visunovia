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
  base: '/',
  build: {
    outDir: '../www_build',
    emptyOutDir: true,
    rollupOptions: {
      output: {
        manualChunks: {
          'baklavajs': ['@baklavajs/core', '@baklavajs/engine', '@baklavajs/renderer-vue'],
          'vue-vendor': ['vue', 'vue-router', 'pinia'],
        }
      }
    }
  },
  server: {
    port: 32423,
    strictPort: false,
    open: false,
    hmr: {
      protocol: 'ws',
      host: 'localhost'
    },
    proxy: {
      '/api': {
        target: 'http://localhost:32523',
        changeOrigin: true,
        secure: false
      }
    }
  }
})
