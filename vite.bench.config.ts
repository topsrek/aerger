// Nur für npm run bench: Stärkevergleich der Computergegner.
import { mergeConfig } from 'vite';
import base from './vite.config';

export default mergeConfig(base, { test: { include: ['test/**/*.bench.test.ts'], exclude: [] } });
