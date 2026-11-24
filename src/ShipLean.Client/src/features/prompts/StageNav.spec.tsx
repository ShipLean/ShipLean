import { expect, test } from '@playwright/experimental-ct-react';
import { PromptsHarness } from './ct/PromptsHarness';
import { listRoutes } from './ct/listRoutes';
import { adminProject } from './ct/promptsData';
import { promptsTestIds as ids } from './testIds';

const at = (prompt: string) => ({
  hooksConfig: { route: `/p/SHIP/ways-of-working?prompt=${prompt}` },
});

test('StageNav_WhenLoaded_ShouldShowSevenStagesWithTheirCounts', async ({ mount, page }) => {
  await listRoutes(page);
  await mount(<PromptsHarness project={adminProject} />, at('issueProposal'));

  await expect(page.getByTestId(ids.stage)).toHaveCount(7);
  const build = page.getByTestId(ids.stage).nth(3);
  await expect(build).toContainText('Build');
  await expect(build.getByTestId(ids.stageStripCount)).toHaveText('3 prompts');
  await expect(build.getByTestId(ids.stageStripCustom)).toHaveText('1 custom');
  await expect(page.getByTestId(ids.stage).nth(1).getByTestId(ids.stageStripCustom)).toHaveCount(0);
});

test('StageNav_ShouldMarkTheStageOfTheSelectedPrompt', async ({ mount, page }) => {
  await listRoutes(page);
  await mount(<PromptsHarness project={adminProject} />, at('report'));
  const strip = page.getByTestId(ids.stage);

  await expect(strip.nth(3)).toHaveAttribute('data-stage', 'build');
  await expect(strip.nth(3)).toHaveAttribute('aria-current', 'true');
  await expect(strip.nth(0)).toHaveAttribute('aria-current', 'false');
  await expect(page.getByTestId(ids.stageChip)).toHaveText(['Lead brief', 'Task brief', 'Report']);
  await expect(page.getByTestId(ids.stageWhen)).toContainText('Each wave runs');
});

test('StageNav_WhenStageHasOnePrompt_ShouldShowNoChipsOnlyTheWhenLine', async ({ mount, page }) => {
  await listRoutes(page);
  await mount(<PromptsHarness project={adminProject} />, at('issueProposal'));

  await expect(page.getByTestId(ids.stageWhen)).toHaveText(
    'Intake. A GitHub issue arrives on a connected repository.',
  );
  await expect(page.getByTestId(ids.stageChip)).toHaveCount(0);
});

test('StageNav_WhenCustomOrUpdated_ShouldDotTheirChips', async ({ mount, page }) => {
  await listRoutes(page);
  await mount(<PromptsHarness project={adminProject} />, at('guide'));

  const chips = page.getByTestId(ids.stageChip);
  await expect(chips).toHaveCount(2);
  await expect(chips.nth(0)).toHaveAttribute('data-status', 'builtInUpdated');
  await expect(chips.nth(1)).toHaveAttribute('data-status', 'builtIn');
});

test('StageStrip_WhenLoaded_ShouldCountCustomsAsTheHeaderDoes', async ({ mount, page }) => {
  await listRoutes(page);
  await mount(<PromptsHarness project={adminProject} />, at('issueProposal'));

  await expect(page.getByTestId(ids.customCountPill)).toHaveText('2 custom');
  const counts = await page.getByTestId(ids.stageStripCustom).allTextContents();
  const sum = counts.reduce((total, text) => total + Number.parseInt(text, 10), 0);
  expect(sum).toBe(2);
});

test('StageStrip_ShouldShadeTheStageOfTheSelectedPrompt', async ({ mount, page }) => {
  await listRoutes(page);
  await mount(<PromptsHarness project={adminProject} />, at('report'));
  const stages = page.getByTestId(ids.stage);
  await expect(stages).toHaveCount(7);

  const fills = await stages.evaluateAll((cells) =>
    cells.map((cell) => getComputedStyle(cell).backgroundColor),
  );
  expect(fills[3]).not.toBe(fills[2]);
  expect(fills[2]).toBe(fills[4]);
  await expect(stages.nth(3)).toHaveAttribute('aria-current', 'true');
});

test('StageStrip_ShouldJoinStagesWithChevrons', async ({ mount, page }) => {
  await listRoutes(page);
  await mount(<PromptsHarness project={adminProject} />, at('report'));

  await expect(page.getByTestId(ids.stage)).toHaveCount(7);
  await expect(page.getByTestId(ids.stageChevron)).toHaveCount(6);
  await expect(page.getByTestId(ids.stage).last().getByTestId(ids.stageChevron)).toHaveCount(0);
});

test('StageChips_WhenCustom_ShouldShowThePurpleDot', async ({ mount, page }) => {
  await listRoutes(page);
  await mount(<PromptsHarness project={adminProject} />, at('report'));

  const chips = page.getByTestId(ids.stageChip);
  await expect(chips).toHaveCount(3);
  const custom = page.locator(`[data-testid="${ids.stageChip}"][data-status="custom"]`);
  await expect(custom).toHaveCount(1);
  await expect(custom.getByTestId(ids.stageChipDot)).toHaveAttribute('data-dot', 'custom');
  await expect(
    page
      .locator(`[data-testid="${ids.stageChip}"][data-status="builtIn"]`)
      .first()
      .getByTestId(ids.stageChipDot),
  ).toHaveCount(0);
});

test('StageChips_WhenUpdated_ShouldShowTheAmberDot', async ({ mount, page }) => {
  await listRoutes(page);
  await mount(<PromptsHarness project={adminProject} />, at('guide'));

  const updated = page.locator(`[data-testid="${ids.stageChip}"][data-status="builtInUpdated"]`);
  await expect(updated).toHaveCount(1);
  await expect(updated.getByTestId(ids.stageChipDot)).toHaveAttribute('data-dot', 'updated');
});
