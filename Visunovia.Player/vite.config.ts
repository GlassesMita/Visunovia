import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  build: {
    outDir: '../www_player_build',
    emptyOutDir: true,
  },
  server: {
    port: 32424,
    strictPort: false,
  },
})