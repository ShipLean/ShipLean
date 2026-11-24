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
const brief = adminPage.stages.flatMap((s) => s.prompts).find((p) => p.key === 'brief')!;

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
  await expect(page.getByTestId(ids.header)).toBeVisible();
  await expect(page.getByTestId(ids.unsavedPill)).toHaveCount(0);
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
  await expect(page.getByTestId(ids.editorStartedFrom)).toHaveText(
    `Started from the ${guide.update!.changedLabel}`,
  );
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

// D15: the note is optional. Replaces PromptEditor_WhenCtrlSWithNoNote_ShouldNotSave and the no-note half of
// PromptEditor_WhenUnchangedOrNoNote_ShouldDisableSave.
test('PromptEditor_WhenNoNote_ShouldStillAllowSave', async ({ mount, page }) => {
  const { posts } = await open(page, mount);

  await page.getByTestId(ids.editorText).fill('# Changed\n');
  await expect(page.getByTestId(ids.editorNote)).toHaveValue('');
  await expect(page.getByTestId(ids.editorSave)).toBeEnabled();
  await page.getByTestId(ids.editorText).press('Control+s');

  await expect.poll(() => posts.length).toBe(1);
  expect(posts[0]).toEqual({ text: '# Changed\n', note: '', source: 'editor' });
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

test('PromptEditor_WhenUnchanged_ShouldDisableSave', async ({ mount, page }) => {
  await open(page, mount);
  const save = page.getByTestId(ids.editorSave);

  await expect(save).toBeDisabled();
  await page.getByTestId(ids.editorNote).fill('A note alone');
  await expect(save).toBeDisabled();
  await page.getByTestId(ids.editorText).fill('# Edited\n');
  await expect(save).toBeEnabled();
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
  await expect(page.getByTestId(ids.unsavedPill)).toBeVisible();
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
  await page.getByTestId(ids.searchInput).fill('Plan reviewer');
  await page.getByTestId(ids.searchInput).press('Enter');
  await expect(page.getByTestId(ids.editorLeaveDialog)).toBeVisible();

  await page.getByTestId(ids.editorLeaveStay).click();
  await expect(page.getByTestId(ids.editorLeaveDialog)).toHaveCount(0);
  await expect(page.getByTestId(ids.editorText)).toHaveValue('# Edited\n');
  await expect(page.getByTestId('harness-search')).toHaveText('prompt=work');

  await page.getByTestId(ids.searchInput).fill('Plan reviewer');
  await page.getByTestId(ids.searchInput).press('Enter');
  await page.getByTestId(ids.editorLeaveDiscard).click();
  await expect(page.getByTestId('harness-search')).toHaveText('prompt=planReviewer');
  await expect(page.getByTestId(ids.editor)).toHaveCount(0);
});

test('PromptEditor_ShouldBeAPlainTextAreaWithoutAToolbar', async ({ mount, page }) => {
  await open(page, mount);
  const text = page.getByTestId(ids.editorText);

  expect(await text.evaluate((el) => el.tagName)).toBe('TEXTAREA');
  await expect(text).toBeVisible();
  // Nothing in the editing area is a control except the side panel's own links: no Markdown toolbar.
  const body = page.getByTestId(ids.editorBody);
  await expect(page.getByTestId(ids.editorSide)).toBeVisible();
  const controls = await body.evaluate(
    (el, side) =>
      [...el.querySelectorAll('button, [role="toolbar"]')].filter(
        (control) => control.closest(`[data-testid="${side}"]`) === null,
      ).length,
    ids.editorSide,
  );
  expect(controls).toBe(0);
  await text.fill('# Typed\n');
  await expect(text).toHaveValue('# Typed\n');
});

test('PromptEditor_ShouldShowEditPreviewAndChangesInTheTabBandWithStartedFrom', async ({
  mount,
  page,
}) => {
  await open(page, mount);
  const edit = page.getByTestId(ids.editorTabEdit);
  const changes = page.getByTestId(ids.editorTabChanges);

  await expect(edit).toHaveAttribute('role', 'tab');
  await expect(edit).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByTestId(ids.editorTabPreview)).toHaveAttribute('aria-selected', 'false');
  await expect(changes).toHaveText('Changes from built-in+0 −0');
  await expect(page.getByTestId(ids.editorStartedFrom)).toHaveText(
    'Started from the built-in of today',
  );

  await page.getByTestId(ids.editorText).fill(`${work.builtInText}One more line.\n`);
  await expect(changes).toHaveText('Changes from built-in+1 −0');
  await changes.click();
  await expect(changes).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByTestId(ids.editorChanges)).toContainText('One more line.');
});

test('PromptEditor_WhenEditingACustomVersion_ShouldSayWhichBuiltInItStartedFrom', async ({
  mount,
  page,
}) => {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await page.route(CUSTOMIZE_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts?prompt=brief' },
  });
  await page.getByTestId(ids.actionEdit).click();

  await expect(page.getByTestId(ids.editorStartedFrom)).toHaveText(
    `Started from the ${brief.custom!.baseLabel}`,
  );
});

test('PromptEditor_ShouldPinNoteCancelAndSaveToTheBottom', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: adminPage }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await page.route(CUSTOMIZE_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={adminProject} />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts?prompt=brief' },
  });
  await page.getByTestId(ids.actionEdit).click();
  // The tags state: an unknown tag adds its warning to a side panel already six placeholders long.
  await page
    .getByTestId(ids.editorText)
    .fill(
      `${brief.custom!.text}\n\n<claudecode>\nWhen a test fails twice, stop and report it.\n</claudecode>\n`,
    );
  await expect(page.getByTestId(ids.checkUnknownTag)).toHaveCount(1);

  // The side panel runs past the bottom of the window…
  const side = await page.getByTestId(ids.editorSide).boundingBox();
  expect(side!.y + side!.height).toBeGreaterThan(900);
  // …and the note, Cancel and Save stay in view.
  await expect(page.getByTestId(ids.editorFooter)).toBeInViewport({ ratio: 1 });
  await expect(page.getByTestId(ids.editorNote)).toBeInViewport({ ratio: 1 });
  await expect(page.getByTestId(ids.editorCancel)).toBeInViewport({ ratio: 1 });
  await expect(page.getByTestId(ids.editorSave)).toBeInViewport({ ratio: 1 });
});
