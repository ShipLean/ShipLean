import { expect, test } from '@playwright/experimental-ct-react';
import { PromptsBadgeHarness } from './ct/PromptsBadgeHarness';
import { PromptsHarness } from './ct/PromptsHarness';
import { listRoutes, listHooks } from './ct/listRoutes';
import { PROMPTS_OPENED_URL, PROMPTS_URL, adminPage, adminProject } from './ct/promptsData';
import { promptsTestIds as ids } from './testIds';

test('PromptsPage_WhenLoaded_ShouldShowTheShellAndSelectTheFirstPrompt', async ({
  mount,
  page,
}) => {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts' },
  });

  await expect(page.getByTestId(ids.page)).toBeVisible();
  await expect(page.getByTestId(ids.stageStrip)).toBeAttached();
  await expect(page.getByTestId(ids.list)).toBeVisible();
  await expect(page.getByTestId(ids.detailName)).toHaveText('Issue proposal');
  await expect(page.getByTestId(ids.tabSource)).toBeVisible();
});

test('PromptsPage_WhenPromptInQuery_ShouldSelectIt', async ({ mount, page }) => {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts?prompt=guide' },
  });

  await expect(page.getByTestId(ids.detailName)).toHaveText('Planning and breakdown');
});

test('PromptsPage_WhenUnknownPromptInQuery_ShouldSelectTheFirst', async ({ mount, page }) => {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts?prompt=nope' },
  });

  await expect(page.getByTestId(ids.detailName)).toHaveText('Issue proposal');
});

test('PromptsPage_WhenPromptSelected_ShouldSetTheQuery', async ({ mount, page }) => {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts' },
  });

  await page.getByTestId(ids.listRow).nth(3).click();

  await expect(page.getByTestId('harness-search')).toHaveText('prompt=planReviewer');
  await expect(page.getByTestId(ids.detailName)).toHaveText('Plan reviewer');
});

test('PromptsPage_WhenOpened_ShouldPostPromptsOpenedOnce', async ({ mount, page }) => {
  let posts = 0;
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => {
    posts += 1;
    return route.fulfill({ status: 204 });
  });
  await mount(<PromptsHarness project={adminProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts' },
  });
  await expect(page.getByTestId(ids.detailName)).toBeVisible();
  await page.getByTestId(ids.listRow).nth(2).click();
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
  await mount(<PromptsHarness project={adminProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts?prompt=work' },
  });
  await page.getByTestId(ids.actionCustomize).click();
  await expect(page.getByTestId(ids.editor)).toBeVisible();

  await page.getByTestId(ids.listRow).nth(3).click();
  await expect(page.getByTestId(ids.detailName)).toHaveText('Plan reviewer');
  await expect(page.getByTestId(ids.editor)).toHaveCount(0);
  await page.getByTestId(ids.listRow).nth(1).click();

  await expect(page.getByTestId(ids.detailName)).toHaveText('Work from idea to done');
  await expect(page.getByTestId(ids.editor)).toHaveCount(0);
  await expect(page.getByTestId(ids.actionCustomize)).toBeVisible();
});

test('PromptsPage_WhenLoadFails_ShouldOfferRetry', async ({ mount, page }) => {
  let fail = true;
  await page.route(PROMPTS_URL, (route) =>
    fail
      ? route.fulfill({ status: 500, json: { error: 'Boom', code: 'internal' } })
      : route.fulfill({ json: adminPage }),
  );
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts' },
  });

  await expect(page.getByTestId(ids.loadFailed)).toBeVisible();
  fail = false;
  await page.getByTestId(ids.retry).click();
  await expect(page.getByTestId(ids.detailName)).toHaveText('Issue proposal');
});

test('PromptsList_WhenLoaded_ShouldGroupTwelvePromptsUnderSevenStages', async ({ mount, page }) => {
  await listRoutes(page);
  await mount(<PromptsHarness project={adminProject} />, listHooks);

  await expect(page.getByTestId(ids.listStage)).toHaveCount(7);
  await expect(page.getByTestId(ids.listRow)).toHaveCount(12);
  await expect(page.getByTestId(ids.stageStripItem)).toHaveCount(7);
  await expect(page.getByTestId(ids.listRowName).filter({ hasText: 'Retro analysis' })).toHaveCount(
    1,
  );
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
  await listRoutes(page);
  await mount(<PromptsHarness project={adminProject} />, listHooks);

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
  await listRoutes(page);
  await mount(<PromptsHarness project={adminProject} />, listHooks);

  await page.getByTestId(ids.listSearch).fill('SPRINT_NAME');

  await expect(page.getByTestId(ids.listRow)).toHaveCount(1);
  await expect(page.getByTestId(ids.listRowName)).toHaveText('Sprint demo video');
  await expect(page.getByTestId(ids.listStage)).toHaveCount(1);

  await page.getByTestId(ids.listSearch).fill('zzz-no-such-prompt');
  await expect(page.getByTestId(ids.listEmpty)).toBeVisible();
  await expect(page.getByTestId(ids.listRow)).toHaveCount(0);
});

test('PromptsList_WhenCustomFilter_ShouldKeepCustomsOnly', async ({ mount, page }) => {
  await listRoutes(page);
  await mount(<PromptsHarness project={adminProject} />, listHooks);

  await page.getByTestId(ids.filterCustom).click();

  await expect(page.getByTestId(ids.filterCustom)).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByTestId(ids.listRow)).toHaveCount(2);
  await page.getByTestId(ids.filterAll).click();
  await expect(page.getByTestId(ids.listRow)).toHaveCount(12);
});

test('PromptsTabBadge_WhenTwoCustoms_ShouldSayTwoCustom', async ({ mount, page }) => {
  await listRoutes(page);
  await mount(<PromptsBadgeHarness project={adminProject} />, listHooks);

  await expect(page.getByTestId(ids.tabBadge)).toHaveText('2 custom');
});
