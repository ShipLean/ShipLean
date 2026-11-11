// Vitest runs every SPA test file on the web platform, as main.tsx does the app.
import { configurePlatform } from '@shiplean/core/platform';
import { webPlatform } from './webPlatform';

configurePlatform(webPlatform);
