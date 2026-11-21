import { expect, test } from '@playwright/experimental-ct-react';
import { PromptsHarness } from './ct/PromptsHarness';
import { PROMPTS_OPENED_URL, PROMPTS_URL, adminPage, adminProject } from './ct/promptsData';
import { promptsTestIds as ids } from './testIds';

// The Prompts tab on a phone (D14): the list stacks above the prompt and the stage strip scrolls inside its own box.
const hooks = { hooksConfig: { route: '/p/SHIP/settings/prompts' } };

test('PromptsPage_WhenOnAPhone_ShouldStackTheListAboveThePrompt', async ({ mount, page }) => {
  expect(page.viewportSize()?.width).toBe(390);
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, hooks);

  const list = (await page.getByTestId(ids.list).boundingBox())!;
  const detail = (await page.getByTestId(ids.detail).boundingBox())!;
  expect(detail.y).toBeGreaterThanOrEqual(list.y + list.height - 1);
  expect(list.width).toBeGreaterThan(300);
  expect(detail.width).toBeGreaterThan(300);
  const row = (await page.getByTestId(ids.listRow).first().boundingBox())!;
  expect(row.height).toBeGreaterThanOrEqual(32);
});

test('StageStrip_WhenOnAPhone_ShouldScrollSidewaysWithoutPageScroll', async ({ mount, page }) => {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, hooks);
  await expect(page.getByTestId(ids.stageStripItem)).toHaveCount(7);

  const strip = page.getByTestId(ids.stageStrip);
  const sizes = await strip.evaluate((el) => ({ scroll: el.scrollWidth, client: el.clientWidth }));
  expect(sizes.scroll).toBeGreaterThan(sizes.client);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(
    true,
  );
});
