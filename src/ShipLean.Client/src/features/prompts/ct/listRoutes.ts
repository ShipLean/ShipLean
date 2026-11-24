import type { Page } from 'playwright';
import { PROMPTS_OPENED_URL, PROMPTS_URL, adminPage } from './promptsData';

export const listHooks = { hooksConfig: { route: '/p/SHIP/ways-of-working' } };

export async function listRoutes(page: Page) {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
}
