import { defineConfig } from '@playwright/test';
export default defineConfig({ testDir: './tests', use: { browserName: 'chromium', viewport: { width: 300, height: 72 }, deviceScaleFactor: 1 } });
