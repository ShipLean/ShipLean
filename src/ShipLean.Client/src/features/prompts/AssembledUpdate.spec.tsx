import { expect, test } from '@playwright/experimental-ct-react';
import type { PromptAssembledDto } from '@shiplean/core/api/prompts';
import assembledJson from '../../../../ShipLean.TestSupport/DtoFixtures/prompts/prompt-assembled.json' with { type: 'json' };
import savedJson from '../../../../ShipLean.TestSupport/DtoFixtures/prompts/prompt-saved.json' with { type: 'json' };
import { openByAdminFlag, openByPageJson } from './ct/openPrompt';
import { guideOf, conflicting } from './ct/promptVariants';
import { adminPage } from './ct/promptsData';
import { promptsTestIds as ids } from './testIds';

const assembled = assembledJson as PromptAssembledDto;
const ASSEMBLED_URL = '**/api/projects/*/prompts/brief/assembled?*';
const GUIDE_URL = '**/api/projects/*/prompts/guide';

test('AssembledTab_ShouldShowTheLayersInOrderWithPlannedOnesMarked', async ({ mount, page }) => {
  await page.route(ASSEMBLED_URL, (route) => route.fulfill({ json: assembled }));
  await openByAdminFlag(mount, page, 'brief');
  await page.getByTestId(ids.tabAssembled).click();

  await expect(page.getByTestId(ids.assembledLayers).getByRole('listitem')).toHaveCount(5);
  await expect(page.getByTestId(ids.assembledLayerThis)).toContainText('Task brief, custom v2');
  await expect(page.getByTestId(ids.assembledLayerPlanned)).toHaveCount(2);
  await expect(page.getByTestId(ids.assembledLayerPlanned).first()).toContainText(
    'Ways of working',
  );
  await expect(page.getByTestId(ids.assembledText)).toContainText('Second line, reworded');
});

test('AssembledTab_WhenAgentChanged_ShouldShowThatAgentsText', async ({ mount, page }) => {
  await page.route(ASSEMBLED_URL, (route) => {
    const agent = new URL(route.request().url()).searchParams.get('agent');
    return route.fulfill({
      json: { ...assembled, agent, text: `Text for ${agent}\n` },
    });
  });
  await openByAdminFlag(mount, page, 'brief');
  await page.getByTestId(ids.tabAssembled).click();
  await expect(page.getByTestId(ids.assembledText)).toContainText('Text for claude-code');

  await page.getByTestId(ids.agentOption).filter({ hasText: 'Cursor' }).click();

  await expect(page.getByTestId(ids.assembledText)).toContainText('Text for cursor');
});

test('UpdateTab_WhenClean_ShouldMarkItAppliesCleanly', async ({ mount, page }) => {
  await openByPageJson(mount, page, 'guide');
  await page.getByTestId(ids.tabUpdate).click();

  await expect(page.getByTestId(ids.updateAppliesCleanly)).toBeVisible();
  await expect(page.getByTestId(ids.updateDiff).getByTestId(ids.diffLine).first()).toBeVisible();
  await expect(page.getByTestId(ids.updateApply)).toBeVisible();
});

test('UpdateTab_WhenApplied_ShouldSaveTheMergedText', async ({ mount, page }) => {
  let body: unknown = null;
  await page.route(`${GUIDE_URL}/versions`, (route) => {
    body = route.request().postDataJSON();
    return route.fulfill({ json: savedJson });
  });
  await openByPageJson(mount, page, 'guide');
  await page.getByTestId(ids.tabUpdate).click();

  await page.getByTestId(ids.updateApply).click();

  await expect
    .poll(() => body)
    .toEqual({
      text: guideOf(adminPage).update!.mergedText,
      note: 'Applied the 20 Nov built-in change',
      source: 'builtInUpdate',
    });
});

test('UpdateTab_WhenApplyRejected_ShouldShowTheError', async ({ mount, page }) => {
  await page.route(`${GUIDE_URL}/versions`, (route) =>
    route.fulfill({ status: 409, json: { error: 'The built-in changed again.' } }),
  );
  await openByPageJson(mount, page, 'guide');
  await page.getByTestId(ids.tabUpdate).click();

  await page.getByTestId(ids.updateApply).click();

  await expect(page.getByTestId(ids.updateApplyError)).toBeVisible();
});

test('UpdateTab_WhenConflicting_ShouldOfferOnlyApplyAndEdit', async ({ mount, page }) => {
  await openByPageJson(mount, page, 'guide', conflicting(adminPage));
  await page.getByTestId(ids.tabUpdate).click();

  await expect(page.getByTestId(ids.updateNeedsMerge)).toContainText('Apply and edit');
  await expect(page.getByTestId(ids.updateAppliesCleanly)).toHaveCount(0);
  await expect(page.getByTestId(ids.updateApply)).toHaveCount(0);
  await expect(page.getByTestId(ids.updateApplyEdit)).toBeVisible();
});

test('UpdateTab_WhenApplyAndEdit_ShouldCallOnApplyAndEdit', async ({ mount, page }) => {
  await openByPageJson(mount, page, 'guide');
  await page.getByTestId(ids.tabUpdate).click();

  await page.getByTestId(ids.updateApplyEdit).click();

  await expect(page.getByTestId(ids.editor)).toHaveAttribute('data-source', 'builtInUpdate');
});

test('UpdateTab_WhenKept_ShouldPostKeep', async ({ mount, page }) => {
  let posted = 0;
  await page.route(`${GUIDE_URL}/update/keep`, (route) => {
    posted++;
    return route.fulfill({ json: savedJson });
  });
  await openByPageJson(mount, page, 'guide');
  await page.getByTestId(ids.tabUpdate).click();

  await page.getByTestId(ids.updateKeep).click();

  await expect.poll(() => posted).toBe(1);
});
