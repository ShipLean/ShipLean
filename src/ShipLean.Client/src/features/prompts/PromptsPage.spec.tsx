import { expect, test } from '@playwright/experimental-ct-react';
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

  expect(posts).toBe(1);
});

test('PromptDetail_WhenMember_ShouldHideChangeActions', async ({ mount, page }) => {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: memberPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={memberProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts?prompt=brief' },
  });

  await expect(page.getByTestId(ids.detailName)).toHaveText('Task brief');
  await expect(page.getByTestId(ids.actions)).toHaveCount(0);
  await expect(page.getByTestId(ids.actionEdit)).toHaveCount(0);
  await expect(page.getByTestId(ids.actionCustomize)).toHaveCount(0);
  await expect(page.getByTestId(ids.actionUseBuiltIn)).toHaveCount(0);
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
