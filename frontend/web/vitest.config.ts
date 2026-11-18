import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

// Tách khỏi vite.config.ts để test không cần .env (API_TARGET) hay proxy.
export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
  },
});
