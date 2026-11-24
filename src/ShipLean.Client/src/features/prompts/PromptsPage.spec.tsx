import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { RouteHistoryHarness } from '../../pages/ct/RouteHistoryHarness';
import { stubProjectApi } from '../../pages/pagesFixtures';
import { PromptsHarness } from './ct/PromptsHarness';
import { listRoutes } from './ct/listRoutes';
import { PROMPTS_OPENED_URL, PROMPTS_URL, adminPage, adminProject } from './ct/promptsData';
import { promptsTestIds as ids } from './testIds';

const at = (prompt?: string) => ({
  hooksConfig: {
    route:
      prompt === undefined ? '/p/SHIP/ways-of-working' : `/p/SHIP/ways-of-working?prompt=${prompt}`,
  },
});
const stage = (page: Page, id: string) =>
  page.locator(`[data-testid="${ids.stage}"][data-stage="${id}"]`);
const chip = (page: Page, key: string) =>
  page.locator(`[data-testid="${ids.stageChip}"][data-prompt="${key}"]`);

test('PromptsPage_WhenLoaded_ShouldShowTheShellAndSelectTheFirstPrompt', async ({
  mount,
  page,
}) => {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, at());

  await expect(page.getByTestId(ids.page)).toBeVisible();
  await expect(page.getByTestId(ids.header)).toContainText('Ways of working');
  await expect(page.getByTestId(ids.stageNav)).toBeVisible();
  await expect(page.getByTestId(ids.detailName)).toHaveText('Issue proposal');
  await expect(page.getByTestId(ids.tabSource)).toBeVisible();
});

test('PromptsPage_WhenPromptInQuery_ShouldSelectIt', async ({ mount, page }) => {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, at('guide'));

  await expect(page.getByTestId(ids.detailName)).toHaveText('Planning and breakdown');
});

test('PromptsPage_WhenUnknownPromptInQuery_ShouldSelectTheFirst', async ({ mount, page }) => {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, at('nope'));

  await expect(page.getByTestId(ids.detailName)).toHaveText('Issue proposal');
});

test('PromptsPage_WhenRedirectedWithoutPrompt_ShouldOpenTheFirstPrompt', async ({
  mount,
  page,
}) => {
  await stubProjectApi(page);
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<RouteHistoryHarness to="/" />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts' },
  });

  await expect(page.getByTestId(ids.detailName)).toHaveText('Issue proposal');
  await expect(stage(page, 'intake')).toHaveAttribute('aria-current', 'true');
});

test('PromptsPage_WhenPromptSelected_ShouldSetTheQuery', async ({ mount, page }) => {
  await listRoutes(page);
  await mount(<PromptsHarness project={adminProject} />, at());

  await stage(page, 'plan').click();
  await chip(page, 'planReviewer').click();

  await expect(page.getByTestId('harness-search')).toHaveText('prompt=planReviewer');
  await expect(page.getByTestId(ids.detailName)).toHaveText('Plan reviewer');
});

test('PromptsPage_WhenStageClicked_ShouldOpenItsFirstPrompt', async ({ mount, page }) => {
  await listRoutes(page);
  await mount(<PromptsHarness project={adminProject} />, at());

  await stage(page, 'build').click();

  await expect(page.getByTestId('harness-search')).toHaveText('prompt=lead');
  await expect(page.getByTestId(ids.detailName)).toHaveText('Lead brief');
});

test('PromptsPage_WhenChipClicked_ShouldOpenThatPrompt', async ({ mount, page }) => {
  await listRoutes(page);
  await mount(<PromptsHarness project={adminProject} />, at('lead'));
  await expect(chip(page, 'lead')).toHaveAttribute('aria-current', 'true');

  await chip(page, 'report').click();

  await expect(page.getByTestId('harness-search')).toHaveText('prompt=report');
  await expect(page.getByTestId(ids.detailName)).toHaveText('Report');
  await expect(chip(page, 'report')).toHaveAttribute('aria-current', 'true');
  await expect(chip(page, 'lead')).toHaveAttribute('aria-current', 'false');
});

test('PromptsPage_WhenOpened_ShouldPostPromptsOpenedOnce', async ({ mount, page }) => {
  let posts = 0;
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => {
    posts += 1;
    return route.fulfill({ status: 204 });
  });
  await mount(<PromptsHarness project={adminProject} />, at());
  await expect(page.getByTestId(ids.detailName)).toBeVisible();
  await stage(page, 'plan').click();
  await expect(page.getByTestId(ids.detailName)).toHaveText('Planning and breakdown');

  await expect.poll(() => posts).toBe(1);
});

test('PromptsPage_WhenAnotherPromptSelected_ShouldNotReopenTheEditorOnReturn', async ({
  mount,
  page,
}) => {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await page.route('**/api/projects/*/usage/prompt-customize-opened', (route) =>
    route.fulfill({ status: 204 }),
  );
  await mount(<PromptsHarness project={adminProject} />, at('work'));
  await page.getByTestId(ids.actionCustomize).click();
  await expect(page.getByTestId(ids.editor)).toBeVisible();

  await stage(page, 'plan').click();
  await chip(page, 'planReviewer').click();
  await expect(page.getByTestId(ids.detailName)).toHaveText('Plan reviewer');
  await expect(page.getByTestId(ids.editor)).toHaveCount(0);
  await stage(page, 'shape').click();

  await expect(page.getByTestId(ids.detailName)).toHaveText('Work from idea to done');
  await expect(page.getByTestId(ids.editor)).toHaveCount(0);
  await expect(page.getByTestId(ids.actionCustomize)).toBeVisible();
});

test('PromptsPage_WhenStageClickedWithUnsavedEdits_ShouldAskFirst', async ({ mount, page }) => {
  await listRoutes(page);
  await page.route('**/api/projects/*/usage/prompt-customize-opened', (route) =>
    route.fulfill({ status: 204 }),
  );
  await mount(<PromptsHarness project={adminProject} />, at('work'));
  await page.getByTestId(ids.actionCustomize).click();
  await page.getByTestId(ids.editorText).fill('# Edited\n');

  await stage(page, 'build').click();
  await expect(page.getByTestId(ids.editorLeaveDialog)).toBeVisible();
  await page.getByTestId(ids.editorLeaveStay).click();
  await expect(page.getByTestId(ids.editorLeaveDialog)).toHaveCount(0);
  await expect(page.getByTestId('harness-search')).toHaveText('prompt=work');

  await stage(page, 'build').click();
  await page.getByTestId(ids.editorLeaveDiscard).click();
  await expect(page.getByTestId('harness-search')).toHaveText('prompt=lead');
});

test('PromptsPage_WhenLoadFails_ShouldOfferRetry', async ({ mount, page }) => {
  let fail = true;
  await page.route(PROMPTS_URL, (route) =>
    fail
      ? route.fulfill({ status: 500, json: { error: 'Boom', code: 'internal' } })
      : route.fulfill({ json: adminPage }),
  );
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, at());

  await expect(page.getByTestId(ids.loadFailed)).toBeVisible();
  fail = false;
  await page.getByTestId(ids.retry).click();
  await expect(page.getByTestId(ids.detailName)).toHaveText('Issue proposal');
});

test('WaysOfWorkingHeader_WhenTwoCustoms_ShouldSayTwoCustom', async ({ mount, page }) => {
  await listRoutes(page);
  await mount(<PromptsHarness project={{ ...adminProject, name: 'Storefront' }} />, at());

  await expect(page.getByTestId(ids.header)).toContainText(
    'The prompts agents in Storefront get, in the order they meet them',
  );
  await expect(page.getByTestId(ids.customCountPill)).toHaveText('2 custom');
});

test('WaysOfWorkingHeader_WhenNoCustoms_ShouldHideThePill', async ({ mount, page }) => {
  await page.route(PROMPTS_URL, (route) =>
    route.fulfill({ json: { ...adminPage, customCount: 0 } }),
  );
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, at());

  await expect(page.getByTestId(ids.header)).toBeVisible();
  await expect(page.getByTestId(ids.customCountPill)).toHaveCount(0);
});

test('WaysOfWorkingHeader_WhenEditorDirty_ShouldShowTheUnsavedPill', async ({ mount, page }) => {
  await listRoutes(page);
  await page.route('**/api/projects/*/usage/prompt-customize-opened', (route) =>
    route.fulfill({ status: 204 }),
  );
  await mount(<PromptsHarness project={adminProject} />, at('work'));
  await page.getByTestId(ids.actionCustomize).click();
  await expect(page.getByTestId(ids.editor)).toBeVisible();
  await expect(page.getByTestId(ids.header)).toBeVisible();
  await expect(page.getByTestId(ids.unsavedPill)).toHaveCount(0);

  await page.getByTestId(ids.editorText).fill('# Edited\n');

  await expect(page.getByTestId(ids.unsavedPill)).toHaveText('● Unsaved custom prompt');
});

test('HeaderSearch_WhenTypedAndEnter_ShouldOpenTheFirstMatch', async ({ mount, page }) => {
  await listRoutes(page);
  await mount(<PromptsHarness project={adminProject} />, at());

  await page.getByTestId(ids.searchInput).fill('SPRINT_NAME');
  await expect(page.getByTestId(ids.searchResult)).toHaveCount(1);
  await expect(page.getByTestId(ids.searchResult)).toHaveText('Sprint demo video');
  await page.getByTestId(ids.searchInput).press('Enter');

  await expect(page.getByTestId('harness-search')).toHaveText('prompt=sprintDemo');
  await expect(page.getByTestId(ids.detailName)).toHaveText('Sprint demo video');
  await expect(page.getByTestId(ids.searchResults)).toHaveCount(0);
});

test('HeaderSearch_WhenResultClicked_ShouldOpenThatPrompt', async ({ mount, page }) => {
  await listRoutes(page);
  await mount(<PromptsHarness project={adminProject} />, at());

  await page.getByTestId(ids.searchInput).fill('reviewer');
  await page.locator(`[data-testid="${ids.searchResult}"][data-prompt="codeReviewer"]`).click();

  await expect(page.getByTestId(ids.detailName)).toHaveText('Code reviewer');
});
