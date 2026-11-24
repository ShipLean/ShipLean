import { expect, test } from '@playwright/experimental-ct-react';
import { PromptsHarness } from './ct/PromptsHarness';
import { PROMPTS_OPENED_URL, PROMPTS_URL, adminPage, adminProject } from './ct/promptsData';
import { promptsTestIds as ids } from './testIds';

// Ways of working on a phone: the stage navigation stacks above the prompt, which runs full width. Under 900px the
// stage select replaces the strip (D7), so nothing scrolls sideways.
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

test('StageNav_WhenOnAPhone_ShouldShowAStageSelectWithCounts', async ({ mount, page }) => {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, hooks);

  const select = page.getByTestId(ids.stageSelect);
  await expect(select).toBeVisible();
  await expect(page.getByTestId(ids.stageStrip)).toBeHidden();
  await expect(select.locator('option')).toHaveCount(7);
  await expect(select.locator('option:checked')).toHaveText('3 · Plan · 2 prompts, 1 custom');
  await expect(select.locator('option').nth(3)).toHaveText('4 · Build · 3 prompts, 1 custom');
  await expect(page.getByTestId(ids.stageChip).first()).toBeVisible();
});

test('StageNav_WhenOnAPhoneAndStageChosen_ShouldOpenItsFirstPrompt', async ({ mount, page }) => {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, hooks);

  await page.getByTestId(ids.stageSelect).selectOption('build');
  await expect(page.getByTestId(ids.stageChip).first()).toHaveAttribute('data-prompt', 'lead');
  await expect(page.getByTestId(ids.stageChip).first()).toHaveAttribute('aria-current', 'true');
  await expect(page.getByTestId(ids.stageSelect).locator('option:checked')).toHaveText(
    '4 · Build · 3 prompts, 1 custom',
  );
});

// Replaces StageStrip_WhenOnAPhone_ShouldScrollSidewaysWithoutPageScroll: D7 retires the strip's sideways scroll.
test('PromptsPage_WhenOnAPhone_ShouldNotScrollSideways', async ({ mount, page }) => {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, hooks);
  await expect(page.getByTestId(ids.stageSelect)).toBeVisible();

  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
    ),
  ).toBe(true);
});
