import { expect, test } from '@playwright/experimental-ct-react';
import { PromptsHarness } from './ct/PromptsHarness';
import { PROMPTS_OPENED_URL, PROMPTS_URL, adminPage, adminProject } from './ct/promptsData';
import { promptsTestIds as ids } from './testIds';

// Ways of working on a phone: the stage navigation stacks above the prompt, which runs full width. The stage strip
// still scrolls inside its own box; the stage select under 900px (D7) replaces it.
const hooks = { hooksConfig: { route: '/p/SHIP/ways-of-working?prompt=guide' } };

test('PromptsPage_WhenOnAPhone_ShouldStackTheStageNavAboveThePrompt', async ({ mount, page }) => {
  expect(page.viewportSize()?.width).toBe(390);
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, hooks);

  const nav = (await page.getByTestId(ids.stageNav).boundingBox())!;
  const detail = (await page.getByTestId(ids.detail).boundingBox())!;
  expect(detail.y).toBeGreaterThanOrEqual(nav.y + nav.height - 1);
  expect(nav.width).toBeGreaterThan(300);
  expect(detail.width).toBeGreaterThan(300);
  const chip = (await page.getByTestId(ids.stageChip).first().boundingBox())!;
  expect(chip.height).toBeGreaterThanOrEqual(32);
});

test('StageStrip_WhenOnAPhone_ShouldScrollSidewaysWithoutPageScroll', async ({ mount, page }) => {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, hooks);
  await expect(page.getByTestId(ids.stage)).toHaveCount(7);

  const strip = page.getByTestId(ids.stageStrip);
  const sizes = await strip.evaluate((el) => ({ scroll: el.scrollWidth, client: el.clientWidth }));
  expect(sizes.scroll).toBeGreaterThan(sizes.client);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
    ),
  ).toBe(true);
});
