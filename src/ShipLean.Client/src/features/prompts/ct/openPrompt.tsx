import type { test } from '@playwright/experimental-ct-react';
import type { PromptsPageDto } from '@shiplean/core/api/prompts';
import { PromptsHarness } from './PromptsHarness';
import {
  PROMPTS_OPENED_URL,
  PROMPTS_URL,
  adminPage,
  adminProject,
  memberPage,
  memberProject,
} from './promptsData';

type Fixtures = Parameters<Parameters<typeof test>[2]>[0];

export async function openByAdminFlag(
  mount: Fixtures['mount'],
  page: Fixtures['page'],
  prompt: string,
  admin = true,
  pageJson = admin ? adminPage : memberPage,
) {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: pageJson }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={admin ? adminProject : memberProject} />, {
    hooksConfig: { route: `/p/SHIP/ways-of-working?prompt=${prompt}` },
  });
}

export async function openByPageJson(
  mount: Fixtures['mount'],
  page: Fixtures['page'],
  prompt: string,
  pageJson: PromptsPageDto = adminPage,
  member = false,
) {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: pageJson }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={member ? memberProject : adminProject} />, {
    hooksConfig: { route: `/p/SHIP/ways-of-working?prompt=${prompt}` },
  });
}
