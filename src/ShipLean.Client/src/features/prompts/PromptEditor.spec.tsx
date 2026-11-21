import type { ReactElement } from 'react';
import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { PromptsHarness } from './ct/PromptsHarness';
import { PROMPTS_OPENED_URL, PROMPTS_URL, adminPage, adminProject } from './ct/promptsData';
import { promptsTestIds as ids } from './testIds';

const CUSTOMIZE_URL = '**/api/projects/*/usage/prompt-customize-opened';
const WORK_VERSIONS = '**/api/projects/*/prompts/work/versions';
const work = adminPage.stages.flatMap((s) => s.prompts).find((p) => p.key === 'work')!;
const guide = adminPage.stages.flatMap((s) => s.prompts).find((p) => p.key === 'guide')!;

interface Posted {
  text: string;
  note: string;
  source: string;
}

type Mount = (
  component: ReactElement,
  options: { hooksConfig: { route: string } },
) => Promise<unknown>;

async function open(page: Page, mount: Mount) {
  const posts: Posted[] = [];
  const customize: string[] = [];
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await page.route(CUSTOMIZE_URL, (route) => {
    customize.push(route.request().url());
    return route.fulfill({ status: 204 });
  });
  await page.route(WORK_VERSIONS, (route) => {
    posts.push(route.request().postDataJSON() as Posted);
    return route.fulfill({
      status: 201,
      json: { key: 'work', status: 'custom', number: 1 },
    });
  });
  await mount(<PromptsHarness project={adminProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts?prompt=work' },
  });
  await page.getByTestId(ids.actionCustomize).click();
  await expect(page.getByTestId(ids.editor)).toBeVisible();
  return { posts, customize };
}

test('PromptEditor_WhenOpened_ShouldPrefillTheCurrentText', async ({ mount, page }) => {
  await open(page, mount);

  await expect(page.getByTestId(ids.editorText)).toHaveValue(work.builtInText);
  await expect(page.getByTestId(ids.editorSave)).toHaveText('Save as custom v1');
  await expect(page.getByTestId(ids.editorDirty)).toHaveCount(0);
});

test('PromptEditor_WhenOpened_ShouldPostCustomizeOpened', async ({ mount, page }) => {
  const { customize } = await open(page, mount);

  await expect.poll(() => customize.length).toBe(1);
});

test('PromptEditor_WhenSaved_ShouldPostTextNoteAndSource', async ({ mount, page }) => {
  const { posts } = await open(page, mount);

  await page.getByTestId(ids.editorText).fill('# Mine\n\nOur rules.\n');
  await page.getByTestId(ids.editorNote).fill('Rewrote it');
  await page.getByTestId(ids.editorSave).click();

  await expect.poll(() => posts.length).toBe(1);
  expect(posts[0]).toEqual({
    text: '# Mine\n\nOur rules.\n',
    note: 'Rewrote it',
    source: 'editor',
  });
  await expect(page.getByTestId(ids.editor)).toHaveCount(0);
});

test('PromptEditor_WhenOpenedFromApplyAndEdit_ShouldPrefillTheMergedTextAndSaveAsBuiltInUpdate', async ({
  mount,
  page,
}) => {
  const posts: Posted[] = [];
  await page.route('**/api/projects/*/prompts/guide/versions', (route) => {
    posts.push(route.request().postDataJSON() as Posted);
    return route.fulfill({ status: 201, json: { key: 'guide', status: 'custom', number: 2 } });
  });
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await page.route(CUSTOMIZE_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts?prompt=guide' },
  });
  await page.getByTestId(ids.updateReview).click();
  await page.getByTestId(ids.updateApplyEdit).click();

  const merged = guide.update!.mergedText;
  await expect(page.getByTestId(ids.editorText)).toHaveValue(merged);
  await expect(page.getByTestId(ids.editorSave)).toHaveText('Save as custom v2');
  await page.getByTestId(ids.editorNote).fill('Took the update');
  await expect(page.getByTestId(ids.editorSave)).toBeEnabled();
  await page.getByTestId(ids.editorSave).click();

  await expect.poll(() => posts.length).toBe(1);
  expect(posts[0]).toEqual({
    text: merged,
    note: 'Took the update',
    source: 'builtInUpdate',
  });
});

test('PromptEditor_WhenCtrlS_ShouldSave', async ({ mount, page }) => {
  const { posts } = await open(page, mount);

  await page.getByTestId(ids.editorNote).fill('Quick fix');
  await page.getByTestId(ids.editorText).fill('# Changed\n');
  await page.getByTestId(ids.editorText).press('Control+s');

  await expect.poll(() => posts.length).toBe(1);
  expect(posts[0].note).toBe('Quick fix');
});

test('PromptEditor_WhenCtrlSWithNoNote_ShouldNotSave', async ({ mount, page }) => {
  const { posts } = await open(page, mount);

  await page.getByTestId(ids.editorText).fill('# Changed\n');
  await page.getByTestId(ids.editorText).press('Control+s');

  await expect(page.getByTestId(ids.editorSave)).toBeDisabled();
  expect(posts).toHaveLength(0);
});

test('PromptEditor_WhenOver64KB_ShouldDisableSave', async ({ mount, page }) => {
  await open(page, mount);

  // 21,846 characters are 65,538 UTF-8 bytes: under 64K characters, over 64 KB.
  await page.getByTestId(ids.editorText).fill('€'.repeat(21846));
  await page.getByTestId(ids.editorNote).fill('Big');

  await expect(page.getByTestId(ids.editorSizeMeter)).toHaveAttribute('data-state', 'over');
  await expect(page.getByTestId(ids.editorSave)).toBeDisabled();

  await page.getByTestId(ids.editorText).fill('€'.repeat(21845));
  await expect(page.getByTestId(ids.editorSizeMeter)).toHaveAttribute('data-state', 'warn');
  await expect(page.getByTestId(ids.editorSave)).toBeEnabled();
});

test('PromptEditor_WhenServerRefusesText_ShouldShowTheErrorAndKeepTheEdits', async ({
  mount,
  page,
}) => {
  await open(page, mount);
  await page.unroute(WORK_VERSIONS);
  await page.route(WORK_VERSIONS, (route) =>
    route.fulfill({
      status: 422,
      json: { code: 'validation', error: 'The text is too large.', field: 'text' },
    }),
  );

  await page.getByTestId(ids.editorText).fill('# Mine\n');
  await page.getByTestId(ids.editorNote).fill('Try');
  await page.getByTestId(ids.editorSave).click();

  await expect(page.getByTestId(ids.editorError)).toHaveText('The text is too large.');
  await expect(page.getByTestId(ids.editorText)).toHaveValue('# Mine\n');
  await expect(page.getByTestId(ids.editorNote)).toHaveValue('Try');
  await expect(page.getByTestId(ids.editor)).toBeVisible();
});

test('PromptEditor_WhenServerRefusesNote_ShouldShowTheErrorNextToTheNote', async ({
  mount,
  page,
}) => {
  await open(page, mount);
  await page.unroute(WORK_VERSIONS);
  await page.route(WORK_VERSIONS, (route) =>
    route.fulfill({
      status: 422,
      json: { code: 'validation', error: 'The note is required.', field: 'note' },
    }),
  );

  await page.getByTestId(ids.editorText).fill('# Mine\n');
  await page.getByTestId(ids.editorNote).fill('x');
  await page.getByTestId(ids.editorSave).click();

  await expect(page.getByTestId(ids.editorSaveError)).toHaveText('The note is required.');
  await expect(page.getByTestId(ids.editorError)).toHaveCount(0);
});

test('PromptEditor_WhenUnchangedOrNoNote_ShouldDisableSave', async ({ mount, page }) => {
  await open(page, mount);
  const save = page.getByTestId(ids.editorSave);

  await expect(save).toBeDisabled();
  await page.getByTestId(ids.editorNote).fill('A note alone');
  await expect(save).toBeDisabled();
  await page.getByTestId(ids.editorText).fill('# Edited\n');
  await expect(save).toBeEnabled();
  await page.getByTestId(ids.editorNote).fill('   ');
  await expect(save).toBeDisabled();
  await page.getByTestId(ids.editorNote).fill('Back');
  await page.getByTestId(ids.editorText).fill(work.builtInText);
  await expect(save).toBeDisabled();
});

test('PromptEditor_ShouldMountTheChecksWithTheCurrentText', async ({ mount, page }) => {
  await open(page, mount);
  const checks = page.getByTestId(ids.checks);

  await expect(checks).toHaveAttribute('data-length', String(work.builtInText.length));
  await page.getByTestId(ids.editorText).fill('12345');
  await expect(checks).toHaveAttribute('data-length', '5');
});

test('PromptEditor_WhenOtherTabsOpened_ShouldPreviewAndShowTheChangesFromBuiltIn', async ({
  mount,
  page,
}) => {
  await open(page, mount);
  await page.route('**/api/markdown/render', (route) =>
    route.fulfill({ json: { html: '<h1>Rendered mine</h1>' } }),
  );
  await page.getByTestId(ids.editorText).fill(`${work.builtInText}Our extra line.\n`);

  await page.getByTestId(ids.editorTabChanges).click();
  await expect(page.getByTestId(ids.editorChanges)).toContainText('Our extra line.');
  await expect(
    page.getByTestId(ids.diffLine).filter({ hasText: 'Our extra line.' }),
  ).toHaveAttribute('data-kind', 'add');

  await page.getByTestId(ids.editorTabPreview).click();
  await expect(page.getByTestId(ids.editorPreview)).toContainText('Rendered mine');

  await page.getByTestId(ids.editorTabEdit).click();
  await expect(page.getByTestId(ids.editorText)).toContainText('Our extra line.');
});

test('PromptEditor_WhenLeavingWithEdits_ShouldAskFirst', async ({ mount, page }) => {
  await open(page, mount);

  await page.getByTestId(ids.editorText).fill('# Edited\n');
  await expect(page.getByTestId(ids.editorDirty)).toBeVisible();
  await page.getByTestId(ids.editorCancel).click();
  await expect(page.getByTestId(ids.editorLeaveDialog)).toBeVisible();

  await page.getByTestId(ids.editorLeaveStay).click();
  await expect(page.getByTestId(ids.editorLeaveDialog)).toHaveCount(0);
  await expect(page.getByTestId(ids.editorText)).toHaveValue('# Edited\n');

  await page.getByTestId(ids.editorCancel).click();
  await page.getByTestId(ids.editorLeaveDiscard).click();
  await expect(page.getByTestId(ids.editor)).toHaveCount(0);
});

test('PromptEditor_WhenLeavingWithoutEdits_ShouldCloseAtOnce', async ({ mount, page }) => {
  await open(page, mount);

  await page.getByTestId(ids.editorCancel).click();

  await expect(page.getByTestId(ids.editorLeaveDialog)).toHaveCount(0);
  await expect(page.getByTestId(ids.editor)).toHaveCount(0);
});

test('PromptsPage_WhenSwitchingPromptWithUnsavedEdits_ShouldAskFirst', async ({ mount, page }) => {
  await open(page, mount);

  await page.getByTestId(ids.editorText).fill('# Edited\n');
  await page.getByTestId(ids.listRow).nth(3).click();
  await expect(page.getByTestId(ids.editorLeaveDialog)).toBeVisible();

  await page.getByTestId(ids.editorLeaveStay).click();
  await expect(page.getByTestId(ids.editorLeaveDialog)).toHaveCount(0);
  await expect(page.getByTestId(ids.editorText)).toHaveValue('# Edited\n');
  await expect(page.getByTestId('harness-search')).toHaveText('prompt=work');

  await page.getByTestId(ids.listRow).nth(3).click();
  await page.getByTestId(ids.editorLeaveDiscard).click();
  await expect(page.getByTestId('harness-search')).toHaveText('prompt=planReviewer');
  await expect(page.getByTestId(ids.editor)).toHaveCount(0);
});
