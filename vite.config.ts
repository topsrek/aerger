/// <reference types="vitest/config" />
import preact from '@preact/preset-vite';
import { defineConfig } from 'vite';

export default defineConfig({
  base: './',
  plugins: [preact()],
  build: { target: 'es2022', assetsInlineLimit: 0 },
  test: { include: ['test/**/*.test.ts'], exclude: ['test/**/*.bench.test.ts'], testTimeout: 60_000 },
});
