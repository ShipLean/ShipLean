import { expect, test } from '@playwright/experimental-ct-react';
import type { PromptDto, PromptsPageDto } from '@shiplean/core/api/prompts';
import savedJson from '../../../../ShipLean.TestSupport/DtoFixtures/prompts/prompt-saved.json' with { type: 'json' };
import { PromptsHarness } from './ct/PromptsHarness';
import {
  PROMPTS_OPENED_URL,
  PROMPTS_URL,
  adminPage,
  adminProject,
  memberPage,
  memberProject,
} from './ct/promptsData';
import { promptsTestIds as ids } from './testIds';

const GUIDE_URL = '**/api/projects/*/prompts/guide';
const BRIEF_URL = '**/api/projects/*/prompts/brief';

const withPrompt = (
  base: PromptsPageDto,
  key: string,
  change: (p: PromptDto) => PromptDto,
): PromptsPageDto => ({
  ...base,
  stages: base.stages.map((s) => ({
    ...s,
    prompts: s.prompts.map((p) => (p.key === key ? change(p) : p)),
  })),
});
const guideOf = (p: PromptsPageDto) =>
  p.stages.flatMap((s) => s.prompts).find((x) => x.key === 'guide')!;
const conflicting = (base: PromptsPageDto) =>
  withPrompt(base, 'guide', (p) => ({
    ...p,
    update: { ...p.update!, appliesCleanly: false, mergedText: '<<<<<<< mine\n' },
  }));

async function open(
  mount: Parameters<Parameters<typeof test>[2]>[0]['mount'],
  page: Parameters<Parameters<typeof test>[2]>[0]['page'],
  prompt: string,
  pageJson: PromptsPageDto = adminPage,
  member = false,
) {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: pageJson }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={member ? memberProject : adminProject} />, {
    hooksConfig: { route: `/p/SHIP/settings/prompts?prompt=${prompt}` },
  });
}

test('UpdateBanner_WhenBuiltInUpdated_ShouldNameBothDates', async ({ mount, page }) => {
  await open(mount, page, 'guide');

  await expect(page.getByTestId(ids.updateBannerText)).toHaveText(
    'The built-in prompt changed on 20 Nov built-in. Agents still get your custom v1, which started from the built-in of 10 Nov.',
  );
});

test('UpdateTab_WhenClean_ShouldMarkItAppliesCleanly', async ({ mount, page }) => {
  await open(mount, page, 'guide');
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
  await open(mount, page, 'guide');
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
  await open(mount, page, 'guide');
  await page.getByTestId(ids.tabUpdate).click();

  await page.getByTestId(ids.updateApply).click();

  await expect(page.getByTestId(ids.updateApplyError)).toBeVisible();
});

test('UpdateTab_WhenConflicting_ShouldOfferOnlyApplyAndEdit', async ({ mount, page }) => {
  await open(mount, page, 'guide', conflicting(adminPage));
  await page.getByTestId(ids.tabUpdate).click();

  await expect(page.getByTestId(ids.updateNeedsMerge)).toContainText('Apply and edit');
  await expect(page.getByTestId(ids.updateAppliesCleanly)).toHaveCount(0);
  await expect(page.getByTestId(ids.updateApply)).toHaveCount(0);
  await expect(page.getByTestId(ids.updateApplyEdit)).toBeVisible();
});

test('UpdateTab_WhenApplyAndEdit_ShouldCallOnApplyAndEdit', async ({ mount, page }) => {
  await open(mount, page, 'guide');
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
  await open(mount, page, 'guide');
  await page.getByTestId(ids.tabUpdate).click();

  await page.getByTestId(ids.updateKeep).click();

  await expect.poll(() => posted).toBe(1);
});

test('UpdateBanner_WhenReviewClicked_ShouldCallOnReview', async ({ mount, page }) => {
  await open(mount, page, 'guide');

  await page.getByTestId(ids.updateReview).click();

  await expect(page.getByTestId(ids.panelUpdate)).toBeVisible();
});

test('UpdateBanner_WhenMember_ShouldShowNoActions', async ({ mount, page }) => {
  await open(mount, page, 'guide', memberPage, true);
  await expect(page.getByTestId(ids.updateBannerText)).toBeVisible();
  await expect(page.getByTestId(ids.updateReview)).toHaveCount(0);
  await page.getByTestId(ids.tabUpdate).click();

  await expect(page.getByTestId(ids.updateDiff)).toBeVisible();
  await expect(page.getByTestId(ids.updateApply)).toHaveCount(0);
  await expect(page.getByTestId(ids.updateApplyEdit)).toHaveCount(0);
  await expect(page.getByTestId(ids.updateKeep)).toHaveCount(0);
});

test('UseBuiltInDialog_WhenConfirmed_ShouldPostUseBuiltIn', async ({ mount, page }) => {
  let posted = 0;
  await page.route(`${BRIEF_URL}/use-built-in`, (route) => {
    posted++;
    return route.fulfill({ json: savedJson });
  });
  await open(mount, page, 'brief');
  await page.getByTestId(ids.actionUseBuiltIn).click();
  await expect(page.getByTestId(ids.useBuiltInDialog)).toContainText('Runs already going keep');

  await page.getByTestId(ids.useBuiltInConfirm).click();

  await expect.poll(() => posted).toBe(1);
  await expect(page.getByTestId(ids.useBuiltInDialog)).toHaveCount(0);
});

test('UseBuiltInDialog_WhenCancelled_ShouldPostNothing', async ({ mount, page }) => {
  let posted = 0;
  await page.route(`${BRIEF_URL}/use-built-in`, (route) => {
    posted++;
    return route.fulfill({ json: savedJson });
  });
  await open(mount, page, 'brief');
  await page.getByTestId(ids.actionUseBuiltIn).click();

  await page.getByTestId(ids.useBuiltInCancel).click();

  await expect(page.getByTestId(ids.useBuiltInDialog)).toHaveCount(0);
  expect(posted).toBe(0);
});
