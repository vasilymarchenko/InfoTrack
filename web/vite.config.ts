import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    proxy: {
      '/api': 'http://localhost:5194',
      '/health': 'http://localhost:5194',
    },
  },
  build: {
    // In Docker (DOCKER_BUILD=1) emit to ./dist so the build stage can COPY it cleanly.
    // In local dev the output goes directly into the API's wwwroot.
    outDir: process.env.DOCKER_BUILD ? 'dist' : '../src/InfoTrack.Api/wwwroot',
    emptyOutDir: true,
  },
})
