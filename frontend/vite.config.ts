import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // .env lives at the repo root (alongside docker-compose.yml), not in frontend/.
  const env = loadEnv(mode, process.cwd() + '/..', '')
  const backendPort = env.BACKEND_PORT || '5050'

  return {
    plugins: [react()],
    server: {
      proxy: {
        // Mirrors the nginx proxy used in the production container so the
        // frontend can always call the backend via a same-origin relative path.
        '/api': {
          target: `http://localhost:${backendPort}`,
          changeOrigin: true,
        },
      },
    },
  }
})
