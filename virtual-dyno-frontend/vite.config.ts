import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// In development, /api is forwarded to the ASP.NET Core API (dotnet run in VirtualDyno.API).
export default defineConfig({
  plugins: [react()],
  build: { chunkSizeWarningLimit: 900 },
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5038',
    },
  },
});
