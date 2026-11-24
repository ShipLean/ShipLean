import { expect, test } from '@playwright/experimental-ct-react';
import { PromptsHarness } from './ct/PromptsHarness';
import { openByPageJson } from './ct/openPrompt';
import { listRoutes } from './ct/listRoutes';
import {
  PROMPTS_OPENED_URL,
  PROMPTS_URL,
  adminPage,
  adminProject,
  memberPage,
  memberProject,
} from './ct/promptsData';
import { promptsTestIds as ids } from './testIds';

const RESUME_URL = '**/api/projects/*/prompts/issueProposal/versions/1/use';

test('PromptDetail_WhenMember_ShouldHideChangeActions', async ({ mount, page }) => {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: memberPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={memberProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts?prompt=brief' },
  });

  await expect(page.getByTestId(ids.detailName)).toHaveText('Task brief');
  await expect(page.getByTestId(ids.actionEdit)).toHaveCount(0);
  await expect(page.getByTestId(ids.actionCustomize)).toHaveCount(0);
  await expect(page.getByTestId(ids.actionUseBuiltIn)).toHaveCount(0);
  await expect(page.getByTestId(ids.actionResume)).toHaveCount(0);
  await expect(page.getByTestId(ids.readOnlyNote)).toContainText(
    'Only project admins can customize',
  );
  await expect(page.getByTestId(ids.readOnlyAdmins)).toHaveText('Joel Dickson or Maria Santos');
  await expect(page.getByTestId(ids.copyMarkdown)).toBeVisible();
});

test('PromptDetail_WhenAdmin_ShouldNotShowTheReadOnlyNote', async ({ mount, page }) => {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts' },
  });

  await expect(page.getByTestId(ids.copyMarkdown)).toBeVisible();
  await expect(page.getByTestId(ids.readOnlyNote)).toHaveCount(0);
});

test('PromptDetail_WhenEditorOpen_ShouldHideTheChromeAndPostCustomizeOnce', async ({
  mount,
  page,
}) => {
  let customizePosts = 0;
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await page.route('**/api/projects/*/usage/prompt-customize-opened', (route) => {
    customizePosts += 1;
    return route.fulfill({ status: 204 });
  });
  await mount(<PromptsHarness project={adminProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts?prompt=work' },
  });
  await expect(page.getByTestId(ids.facts)).toBeAttached();

  await page.getByTestId(ids.actionCustomize).click();

  await expect(page.getByTestId(ids.editor)).toBeVisible();
  await expect(page.getByTestId(ids.actions)).toHaveCount(0);
  await expect(page.getByTestId(ids.facts)).toHaveCount(0);
  await expect(page.getByTestId(ids.updateBanner)).toHaveCount(0);
  await expect.poll(() => customizePosts).toBe(1);
});

test('PromptDetail_WhenAdminOnABuiltIn_ShouldOfferCustomize', async ({ mount, page }) => {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts?prompt=work' },
  });

  await expect(page.getByTestId(ids.actionCustomize)).toBeVisible();
  await expect(page.getByTestId(ids.actionUseBuiltIn)).toHaveCount(0);
});

test('PromptDetail_WhenAdminOnACustom_ShouldOfferEditAndUseBuiltIn', async ({ mount, page }) => {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts?prompt=brief' },
  });

  await expect(page.getByTestId(ids.actionEdit)).toBeVisible();
  await expect(page.getByTestId(ids.actionUseBuiltIn)).toBeVisible();
  await expect(page.getByTestId(ids.actionCustomize)).toHaveCount(0);

  await page.getByTestId(ids.actionUseBuiltIn).click();
  await expect(page.getByTestId(ids.useBuiltInDialog)).toBeVisible();
});

test('PromptDetail_WhenResumeClicked_ShouldUseTheCustomVersionNumber', async ({ mount, page }) => {
  const posted: string[] = [];
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await page.route(RESUME_URL, (route) => {
    posted.push(route.request().method());
    return route.fulfill({ json: { key: 'issueProposal', status: 'custom', inUseVersion: 1 } });
  });
  await mount(<PromptsHarness project={adminProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts' },
  });

  await expect(page.getByTestId(ids.actionResume)).toContainText('Use custom v1 again');
  await page.getByTestId(ids.actionResume).click();

  await expect.poll(() => posted).toEqual(['POST']);
  await expect(page.getByTestId(ids.actionError)).toHaveCount(0);
});

test('PromptDetail_WhenResumeFails_ShouldShowTheError', async ({ mount, page }) => {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await page.route(RESUME_URL, (route) =>
    route.fulfill({ status: 404, json: { error: 'That version is gone.', code: 'not_found' } }),
  );
  await mount(<PromptsHarness project={adminProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts' },
  });

  await page.getByTestId(ids.actionResume).click();

  await expect(page.getByTestId(ids.actionError)).toHaveText('That version is gone.');
});

test('PromptFacts_WhenSelected_ShouldShowWhenServedReadAndPlaceholders', async ({
  mount,
  page,
}) => {
  await listRoutes(page);
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
  await listRoutes(page);
  await mount(<PromptsHarness project={adminProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts?prompt=sprintDemo' },
  });

  await expect(page.getByTestId(ids.factInsertedParts)).toContainText('intro');
  await expect(page.getByTestId(ids.factInsertedParts)).toContainText('start');
  await page.getByTestId(ids.listRow).nth(0).click();
  await expect(page.getByTestId(ids.factInsertedParts)).toHaveCount(0);
  await expect(page.getByTestId(ids.factNote)).toContainText('Replaces the Proposal instruct');
});

test('UpdateBanner_WhenBuiltInUpdated_ShouldNameBothDates', async ({ mount, page }) => {
  await openByPageJson(mount, page, 'guide');

  await expect(page.getByTestId(ids.updateBannerText)).toHaveText(
    'The built-in prompt has changed (the 20 Nov built-in). Agents still get your custom v1, which started from the built-in of 10 Nov, so they miss the change until you take it.',
  );
});

test('UpdateBanner_WhenReviewClicked_ShouldCallOnReview', async ({ mount, page }) => {
  await openByPageJson(mount, page, 'guide');

  await page.getByTestId(ids.updateReview).click();

  await expect(page.getByTestId(ids.panelUpdate)).toBeVisible();
});

test('UpdateBanner_WhenMember_ShouldShowNoActions', async ({ mount, page }) => {
  await openByPageJson(mount, page, 'guide', memberPage, true);
  await expect(page.getByTestId(ids.updateBannerText)).toBeVisible();
  await expect(page.getByTestId(ids.updateReview)).toHaveCount(0);
  await page.getByTestId(ids.tabUpdate).click();

  await expect(page.getByTestId(ids.updateDiff)).toBeVisible();
  await expect(page.getByTestId(ids.updateApply)).toHaveCount(0);
  await expect(page.getByTestId(ids.updateApplyEdit)).toHaveCount(0);
  await expect(page.getByTestId(ids.updateKeep)).toHaveCount(0);
});
