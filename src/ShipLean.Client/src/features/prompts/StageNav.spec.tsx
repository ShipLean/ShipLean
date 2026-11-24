import { expect, test } from '@playwright/experimental-ct-react';
import { PromptsBadgeHarness } from './ct/PromptsBadgeHarness';
import { PromptsHarness } from './ct/PromptsHarness';
import { listRoutes, listHooks } from './ct/listRoutes';
import { adminProject } from './ct/promptsData';
import { promptsTestIds as ids } from './testIds';

test('StageStrip_WhenStageClicked_ShouldMarkIt', async ({ mount, page }) => {
  await listRoutes(page);
  await mount(<PromptsHarness project={adminProject} />, listHooks);
  const strip = page.getByTestId(ids.stageStripItem);
  await expect(strip.nth(5)).toHaveAttribute('aria-current', 'false');

  await strip.nth(5).click();

  await expect(strip.nth(5)).toHaveAttribute('aria-current', 'true');
  await expect(strip.nth(0)).toHaveAttribute('aria-current', 'false');
  await expect(page.getByTestId(ids.listStage).nth(5)).toBeInViewport();
  await strip.nth(5).click();
  await expect(strip.nth(5)).toHaveAttribute('aria-current', 'false');
});

test('StageStrip_WhenLoaded_ShouldCountCustomsAsTheTabBadgeDoes', async ({ mount, page }) => {
  await listRoutes(page);
  await mount(<PromptsBadgeHarness project={adminProject} />, listHooks);

  await expect(page.getByTestId(ids.tabBadge)).toHaveText('2 custom');
  const counts = await page.getByTestId(ids.stageStripCustom).allTextContents();
  const sum = counts.reduce((total, text) => total + Number.parseInt(text, 10), 0);
  expect(sum).toBe(2);
});
