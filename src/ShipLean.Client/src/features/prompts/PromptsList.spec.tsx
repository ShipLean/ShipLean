import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { PromptsBadgeHarness } from './ct/PromptsBadgeHarness';
import { PromptsHarness } from './ct/PromptsHarness';
import { PROMPTS_OPENED_URL, PROMPTS_URL, adminPage, adminProject } from './ct/promptsData';
import { promptsTestIds as ids } from './testIds';

const hooks = { hooksConfig: { route: '/p/SHIP/settings/prompts' } };

async function routes(page: Page) {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
}

test('PromptsList_WhenLoaded_ShouldGroupElevenPromptsUnderSevenStages', async ({ mount, page }) => {
  await routes(page);
  await mount(<PromptsHarness project={adminProject} />, hooks);

  await expect(page.getByTestId(ids.listStage)).toHaveCount(7);
  await expect(page.getByTestId(ids.listRow)).toHaveCount(11);
  await expect(page.getByTestId(ids.stageStripItem)).toHaveCount(7);
  await expect(page.getByTestId(ids.listStageWhen).first()).toHaveText(
    'A GitHub issue arrives on a connected repository.',
  );
  const build = page.getByTestId(ids.stageStripItem).nth(3);
  await expect(build).toContainText('Build');
  await expect(build.getByTestId(ids.stageStripCount)).toHaveText('3 prompts');
  await expect(build.getByTestId(ids.stageStripCustom)).toHaveText('1 custom');
  await expect(
    page.getByTestId(ids.stageStripItem).nth(1).getByTestId(ids.stageStripCustom),
  ).toHaveCount(0);
});

test('PromptsList_WhenCustomAndUpdated_ShouldShowTheirChips', async ({ mount, page }) => {
  await routes(page);
  await mount(<PromptsHarness project={adminProject} />, hooks);

  const status = (n: number) => page.getByTestId(ids.listRow).nth(n).getByTestId(ids.listRowChip);
  await expect(status(0)).toHaveText('Using built-in');
  await expect(status(1)).toHaveText('Built-in');
  await expect(status(2)).toHaveText('Built-in updated');
  await expect(status(2)).toHaveAttribute('data-status', 'builtInUpdated');
  await expect(status(5)).toHaveText('Custom v2');
  await expect(status(5)).toHaveAttribute('data-status', 'custom');
  await expect(page.getByTestId(ids.listRowFile).nth(1)).toHaveText('work.md');
});

test('PromptsList_WhenSearchedForAPlaceholder_ShouldKeepOnlyMatches', async ({ mount, page }) => {
  await routes(page);
  await mount(<PromptsHarness project={adminProject} />, hooks);

  await page.getByTestId(ids.listSearch).fill('SPRINT_NAME');

  await expect(page.getByTestId(ids.listRow)).toHaveCount(1);
  await expect(page.getByTestId(ids.listRowName)).toHaveText('Sprint demo video');
  await expect(page.getByTestId(ids.listStage)).toHaveCount(1);

  await page.getByTestId(ids.listSearch).fill('zzz-no-such-prompt');
  await expect(page.getByTestId(ids.listEmpty)).toBeVisible();
  await expect(page.getByTestId(ids.listRow)).toHaveCount(0);
});

test('PromptsList_WhenCustomFilter_ShouldKeepCustomsOnly', async ({ mount, page }) => {
  await routes(page);
  await mount(<PromptsHarness project={adminProject} />, hooks);

  await page.getByTestId(ids.filterCustom).click();

  await expect(page.getByTestId(ids.filterCustom)).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByTestId(ids.listRow)).toHaveCount(3);
  await page.getByTestId(ids.filterAll).click();
  await expect(page.getByTestId(ids.listRow)).toHaveCount(11);
});

test('StageStrip_WhenStageClicked_ShouldMarkIt', async ({ mount, page }) => {
  await routes(page);
  await mount(<PromptsHarness project={adminProject} />, hooks);
  const strip = page.getByTestId(ids.stageStripItem);
  await expect(strip.nth(5)).toHaveAttribute('aria-current', 'false');

  await strip.nth(5).click();

  await expect(strip.nth(5)).toHaveAttribute('aria-current', 'true');
  await expect(strip.nth(0)).toHaveAttribute('aria-current', 'false');
  await expect(page.locator('#stage-retro')).toBeInViewport();
  await strip.nth(5).click();
  await expect(strip.nth(5)).toHaveAttribute('aria-current', 'false');
});

test('PromptFacts_WhenSelected_ShouldShowWhenServedReadAndPlaceholders', async ({
  mount,
  page,
}) => {
  await routes(page);
  await mount(<PromptsHarness project={adminProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts?prompt=brief' },
  });

  await expect(page.getByTestId(ids.factWhenRuns)).not.toBeEmpty();
  await expect(page.getByTestId(ids.factServedBy)).not.toBeEmpty();
  await expect(page.getByTestId(ids.factReadBy)).not.toBeEmpty();
  await expect(page.getByTestId(ids.placeholderChip)).toHaveText([
    '{{common_brief}}',
    '{{task_key}}',
    '{{task_title}}',
    '{{task_brief}}',
    '{{shared_branch}}',
    '{{report_template}}',
  ]);
  await expect(page.getByTestId(ids.factInsertedParts)).toHaveCount(0);
});

test('PromptFacts_WhenSprintDemo_ShouldListInsertedParts', async ({ mount, page }) => {
  await routes(page);
  await mount(<PromptsHarness project={adminProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts?prompt=sprintDemo' },
  });

  await expect(page.getByTestId(ids.factInsertedParts)).toContainText('intro');
  await expect(page.getByTestId(ids.factInsertedParts)).toContainText('start');
  await page.getByTestId(ids.listRow).nth(0).click();
  await expect(page.getByTestId(ids.factInsertedParts)).toHaveCount(0);
  await expect(page.getByTestId(ids.factNote)).toContainText('Replaces the Proposal instruct');
});

test('PromptsTabBadge_WhenTwoCustoms_ShouldSayTwoCustom', async ({ mount, page }) => {
  await routes(page);
  await mount(<PromptsBadgeHarness project={adminProject} />, hooks);

  await expect(page.getByTestId(ids.tabBadge)).toHaveText('2 custom');
});
