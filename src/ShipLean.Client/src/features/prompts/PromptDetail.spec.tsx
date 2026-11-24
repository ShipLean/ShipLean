import { expect, test } from '@playwright/experimental-ct-react';
import { PromptsHarness } from './ct/PromptsHarness';
import historyJson from '../../../../ShipLean.TestSupport/DtoFixtures/prompts/prompt-history.json' with { type: 'json' };
import savedJson from '../../../../ShipLean.TestSupport/DtoFixtures/prompts/prompt-saved.json' with { type: 'json' };
import type { Page } from 'playwright';
import { openByAdminFlag, openByPageJson } from './ct/openPrompt';
import { listRoutes } from './ct/listRoutes';
import { withPrompt } from './ct/promptVariants';
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
  await page.locator(`[data-testid="${ids.stage}"][data-stage="intake"]`).click();
  await expect(page.getByTestId(ids.factInsertedParts)).toHaveCount(0);
  await expect(page.getByTestId(ids.factNote)).toContainText('Replaces the Proposal instruct');
});

test('UpdateBanner_WhenBuiltInUpdated_ShouldNameBothDates', async ({ mount, page }) => {
  await openByPageJson(mount, page, 'guide');

  await expect(page.getByTestId(ids.updateBannerText)).toHaveText(
    'The built-in prompt changed on 20 Nov. Agents still get your custom v1, which started from the 10 Nov built-in, so they miss the change until you take it.',
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

test('PromptDetail_ShouldShowNameThenStatusChipThenFile', async ({ mount, page }) => {
  await openByAdminFlag(mount, page, 'brief');

  const name = await page.getByTestId(ids.detailName).boundingBox();
  const chip = await page.getByTestId(ids.detailChip).boundingBox();
  const file = await page.getByTestId(ids.detailFile).boundingBox();
  expect(name).not.toBeNull();
  expect(chip).not.toBeNull();
  expect(file).not.toBeNull();
  expect(name!.x).toBeLessThan(chip!.x);
  expect(chip!.x).toBeLessThan(file!.x);
});

test('PromptDetail_WhenBuiltIn_ShouldOfferCustomizeTopRight', async ({ mount, page }) => {
  await openByAdminFlag(mount, page, 'work');

  const customize = page.getByTestId(ids.actionCustomize);
  await expect(customize).toHaveText('Customize for this project');
  const button = await customize.boundingBox();
  const name = await page.getByTestId(ids.detailName).boundingBox();
  const detail = await page.getByTestId(ids.detail).boundingBox();
  const facts = await page.getByTestId(ids.facts).boundingBox();
  // On the title row, above the facts, in the right half of the panel.
  expect(button!.y).toBeLessThan(facts!.y);
  expect(button!.y + button!.height).toBeGreaterThan(name!.y);
  expect(button!.x).toBeGreaterThan(detail!.x + detail!.width / 2);
});

test('PromptDetail_WhenCustom_ShouldOfferEditUseBuiltInAndCopyMarkdown', async ({
  mount,
  page,
}) => {
  await openByAdminFlag(mount, page, 'brief');

  await expect(page.getByTestId(ids.actionEdit)).toHaveText('Edit');
  await expect(page.getByTestId(ids.actionUseBuiltIn)).toHaveText('Use built-in');
  await expect(page.getByTestId(ids.copyMarkdown)).toHaveText('Copy Markdown');
  const actions = await page.getByTestId(ids.actions).boundingBox();
  const facts = await page.getByTestId(ids.facts).boundingBox();
  expect(actions!.y + actions!.height).toBeLessThanOrEqual(facts!.y);
});

test('PromptFacts_WhenBackticks_ShouldRenderCodeChips', async ({ mount, page }) => {
  const edited = withPrompt(adminPage, 'brief', (p) => ({
    ...p,
    whenRuns: 'From `pick_up` to `report_task`, and an unpaired ` stays text.',
    servedBy: '`get_brief` on a task',
  }));
  await openByAdminFlag(mount, page, 'brief', true, edited);

  await expect(page.getByTestId(ids.factServedBy)).toHaveText('get_brief on a task');
  await expect(page.getByTestId(ids.factServedBy).getByTestId(ids.factCode)).toHaveText([
    'get_brief',
  ]);
  await expect(page.getByTestId(ids.factWhenRuns).getByTestId(ids.factCode)).toHaveText([
    'pick_up',
    'report_task',
  ]);
  await expect(page.getByTestId(ids.factWhenRuns)).toContainText('an unpaired ` stays text.');
  await expect(page.getByTestId(ids.factReadBy)).not.toBeEmpty();
  await expect(page.getByTestId(ids.factReadBy).getByTestId(ids.factCode)).toHaveCount(0);
});

test('PromptDetail_WhenCustomInUse_ShouldShowTheInUseCallout', async ({ mount, page }) => {
  await openByAdminFlag(mount, page, 'brief');

  await expect(page.getByTestId(ids.inUseCallout)).toHaveText(
    'Agents in this project get custom v2 (Joel Dickson, 18 Nov) instead of the built-in.',
  );
  await expect(page.getByTestId(ids.inUseCallout)).toHaveAttribute('data-tone', 'purple');
});

test('PromptDetail_WhenUsingBuiltIn_ShouldSayTheCustomIsKeptInHistory', async ({ mount, page }) => {
  await openByAdminFlag(mount, page, 'issueProposal');

  await expect(page.getByTestId(ids.pausedCallout)).toHaveText(
    'Agents get the built-in prompt. Your custom v1 is kept in History.',
  );
  await expect(page.getByTestId(ids.inUseCallout)).toHaveCount(0);
});

test('UpdateBanner_WhenAdmin_ShouldOfferReviewAndKeep', async ({ mount, page }) => {
  let posted = 0;
  await page.route('**/api/projects/*/prompts/guide/update/keep', (route) => {
    posted++;
    return route.fulfill({ json: savedJson });
  });
  await openByPageJson(mount, page, 'guide');

  await expect(page.getByTestId(ids.updateReview)).toHaveText('Review the change');
  await page.getByTestId(ids.updateBannerKeep).click();

  await expect.poll(() => posted).toBe(1);
  await expect(page.getByTestId(ids.updateBannerKeepError)).toHaveCount(0);
});

test('UpdateBanner_WhenKeepFails_ShouldShowTheError', async ({ mount, page }) => {
  await page.route('**/api/projects/*/prompts/guide/update/keep', (route) =>
    route.fulfill({ status: 409, json: { error: 'The built-in moved again.', code: 'conflict' } }),
  );
  await openByPageJson(mount, page, 'guide');

  await page.getByTestId(ids.updateBannerKeep).click();

  await expect(page.getByTestId(ids.updateBannerKeepError)).toHaveText('The built-in moved again.');
});

test('PromptDetail_WhenMember_ShouldShowTheInfoCalloutNamingTheAdmins', async ({ mount, page }) => {
  await openByAdminFlag(mount, page, 'brief', false);

  const note = page.getByTestId(ids.readOnlyNote);
  await expect(note).toHaveText(
    'You can read every prompt. Only project admins can customize them: ask Joel Dickson or Maria Santos.',
  );
  await expect(note).toHaveAttribute('data-tone', 'info');
});

test('PromptDetail_ShouldShowCountsOnYourChangesAndHistory', async ({ mount, page }) => {
  let historyGets = 0;
  await page.route('**/api/projects/*/prompts/brief/history', (route) => {
    historyGets++;
    return route.fulfill({ json: historyJson });
  });
  await openByAdminFlag(mount, page, 'brief');

  // The fixture's patch for brief rewords one line: one added, one removed.
  await expect(page.getByTestId(ids.tabYourChanges)).toHaveText('Your changes+1 −1');
  await expect(page.getByTestId(ids.tabHistory)).toHaveText(
    `History${historyJson.versions.length}`,
  );
  // One read for the count while the History tab is still closed.
  expect(historyGets).toBe(1);
});

test('PromptDetail_WhenBuiltIn_ShouldShowNoCountsNoHistoryTabAndAskForNoHistory', async ({
  mount,
  page,
}) => {
  let historyGets = 0;
  await page.route('**/api/projects/*/prompts/work/history', (route) => {
    historyGets++;
    return route.fulfill({ json: historyJson });
  });
  await openByAdminFlag(mount, page, 'work');

  await expect(page.getByTestId(ids.tabSource)).toBeVisible();
  await expect(page.getByTestId(ids.tabHistory)).toHaveCount(0);
  await expect(page.getByTestId(ids.tabYourChanges)).toHaveCount(0);
  expect(historyGets).toBe(0);
});

test('PromptDetail_WhenCustom_ShouldShowThePurpleDotInTheStatusChip', async ({ mount, page }) => {
  await openByAdminFlag(mount, page, 'brief');

  const dot = page.getByTestId(ids.chipCustom).getByTestId(ids.detailChipDot);
  await expect(dot).toBeVisible();
  await expect(dot).toHaveCSS('background-color', await cssColor(page, '--color-purple'));
});

test('PromptDetail_WhenBuiltIn_ShouldShowNoDotInTheStatusChip', async ({ mount, page }) => {
  await openByAdminFlag(mount, page, 'work');

  await expect(page.getByTestId(ids.chipBuiltIn)).toBeVisible();
  await expect(page.getByTestId(ids.detailChipDot)).toHaveCount(0);
});

/** A colour token as the browser computes it, so the dot is compared with the token, not a copied value. */
async function cssColor(page: Page, token: string): Promise<string> {
  return page.evaluate((name) => {
    const probe = document.createElement('span');
    probe.style.backgroundColor = `var(${name})`;
    document.body.append(probe);
    const color = getComputedStyle(probe).backgroundColor;
    probe.remove();
    return color;
  }, token);
}

test('PromptDetail_WhenOnAPhone_ShouldKeepFactsInTwoColumnsAndEditBesideTheTitle', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await openByAdminFlag(mount, page, 'brief');

  // Facts: values in the second column (labels take the first 112px), the first one on the list's top line.
  const facts = (await page.getByTestId(ids.facts).boundingBox())!;
  const whenRuns = (await page.getByTestId(ids.factWhenRuns).boundingBox())!;
  const servedBy = (await page.getByTestId(ids.factServedBy).boundingBox())!;
  expect(whenRuns.x).toBeGreaterThanOrEqual(facts.x + 112);
  expect(servedBy.x).toBe(whenRuns.x);
  expect(whenRuns.y).toBeLessThanOrEqual(facts.y + 2);

  // Edit on the title row at the right; Use built-in and Copy Markdown on the next line, from the left.
  const name = (await page.getByTestId(ids.detailName).boundingBox())!;
  const edit = (await page.getByTestId(ids.actionEdit).boundingBox())!;
  const useBuiltIn = (await page.getByTestId(ids.actionUseBuiltIn).boundingBox())!;
  const copy = (await page.getByTestId(ids.copyMarkdown).boundingBox())!;
  const detail = (await page.getByTestId(ids.detail).boundingBox())!;
  expect(edit.y).toBeLessThan(name.y + name.height);
  expect(edit.x + edit.width).toBeGreaterThan(detail.x + detail.width - 24);
  expect(useBuiltIn.y).toBeGreaterThanOrEqual(edit.y + edit.height);
  expect(useBuiltIn.x).toBeLessThanOrEqual(name.x + 1);
  expect(Math.abs(copy.y - useBuiltIn.y)).toBeLessThanOrEqual(1);
  expect(copy.x).toBeGreaterThan(useBuiltIn.x);
});
