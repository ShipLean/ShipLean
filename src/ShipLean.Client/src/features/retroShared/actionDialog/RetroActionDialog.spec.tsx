import { expect, test } from '@playwright/experimental-ct-react';
import type { RetroActionDialogDto, RetroPointDto } from '@shiplean/core/api/retros';
import { componentTestIds } from '../../../components/testIds';
import { actionDialog, stubRetros } from '../../retros/ct/retroFixtures';
import { retroSharedTestIds as ids } from '../testIds';
import { RetroActionDialogHarness } from './ct/RetroActionDialogHarness';

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

const selectedPoint = actionDialog.selected[0];
const wentBadlyPoint = actionDialog.offered.find(
  (p) => p.category === 'wentBadly',
) as RetroPointDto;
const notInPlanPoint = actionDialog.offered.find(
  (p) => p.category === 'notInPlan',
) as RetroPointDto;
const planTheme = notInPlanPoint.theme as NonNullable<RetroPointDto['theme']>;

const improvePoint: RetroPointDto = {
  ...wentBadlyPoint,
  id: 'pt-improve',
  category: 'improve',
  text: 'Show the review checklist in the Feature template.',
  theme: null,
};

async function serveDialog(page: Page, dialog: RetroActionDialogDto) {
  await page.route(
    (url) => url.pathname.endsWith('/retros/action-dialog'),
    (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(dialog) }),
  );
}

const open = async (page: Page) => {
  await page.getByRole('button', { name: 'open' }).click();
  await expect(page.getByTestId(ids.retroActionSave)).toBeVisible();
};

test('RetroActionDialog_WhenOpened_ShouldFetchWithThePointIds', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await mount(<RetroActionDialogHarness />);
  await expect(page.getByTestId(ids.retroActionDialog)).toHaveCount(0);
  await page.getByRole('button', { name: 'open' }).click();
  await expect(page.getByTestId(ids.retroActionDialog)).toBeVisible();
  await expect(page.getByTestId(ids.retroActionSave)).toBeVisible();
  await expect.poll(() => stub.reads('/retros/action-dialog').length).toBe(1);
  const query = new URLSearchParams(stub.reads('/retros/action-dialog')[0].query);
  expect(query.getAll('point')).toEqual(['pt-1', 'pt-2']);
  expect(query.get('item')).toBe('item-1');
});

test('lists Selected then the three categories', async ({ mount, page }) => {
  await stubRetros(page);
  await serveDialog(page, {
    ...actionDialog,
    offered: [notInPlanPoint, wentBadlyPoint, improvePoint].sort(
      (a, b) =>
        ['improve', 'wentBadly', 'notInPlan'].indexOf(a.category) -
        ['improve', 'wentBadly', 'notInPlan'].indexOf(b.category),
    ),
  });
  await mount(<RetroActionDialogHarness />);
  await open(page);
  const dialog = page.getByTestId(ids.retroActionDialog);
  await expect(dialog.getByTestId(ids.retroActionPick)).toHaveCount(4);
  await expect(dialog.getByTestId(ids.retroActionPick)).toHaveText([
    new RegExp(`^${selectedPoint.text}`),
    new RegExp(`^${improvePoint.text}`),
    new RegExp(`^${wentBadlyPoint.text}`),
    new RegExp(`^${notInPlanPoint.text}`),
  ]);
  for (const heading of [
    'Selected',
    'Can be improved, no action item yet',
    'Went badly, no action item yet',
    'Not in the plan, no action item yet',
  ]) {
    await expect(dialog.getByText(heading, { exact: true })).toBeVisible();
  }
  await expect(dialog.getByTestId(ids.retroActionPick).first().getByRole('checkbox')).toBeChecked();
  await expect(
    dialog.getByTestId(ids.retroActionPick).nth(1).getByRole('checkbox'),
  ).not.toBeChecked();
  await expect(dialog.getByTestId(ids.retroActionTitle)).toHaveValue(
    selectedPoint.text.slice(0, 80),
  );
});

test('theme follows the picks until chosen', async ({ mount, page }) => {
  await stubRetros(page);
  const second: RetroPointDto = { ...notInPlanPoint, id: 'pt-plan-2', text: 'A second plan gap.' };
  await serveDialog(page, { ...actionDialog, offered: [notInPlanPoint, second] });
  await mount(<RetroActionDialogHarness />);
  await open(page);
  const theme = page.getByTestId(ids.retroActionTheme);
  const picks = page.getByTestId(ids.retroActionPick);
  const selectedTheme = selectedPoint.theme as NonNullable<RetroPointDto['theme']>;
  await expect(theme).toHaveValue(selectedTheme.id);
  await picks.nth(1).getByRole('checkbox').check();
  await expect(theme).toHaveValue(selectedTheme.id);
  await picks.nth(2).getByRole('checkbox').check();
  await expect(theme).toHaveValue(planTheme.id);
  await theme.selectOption(selectedTheme.id);
  await picks.nth(0).getByRole('checkbox').uncheck();
  await expect(theme).toHaveValue(selectedTheme.id);
  await expect(
    page
      .getByTestId(ids.retroActionDialog)
      .getByText('Verdicts judge this theme.', { exact: true }),
  ).toBeVisible();
});

test('Add to existing adds the points', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await mount(<RetroActionDialogHarness />);
  await open(page);
  await page.getByTestId(ids.retroActionModeExisting).click();
  await expect(page.getByTestId(ids.retroActionTitle)).toHaveCount(0);
  await expect(page.getByTestId(ids.retroActionExisting)).toHaveValue(
    actionDialog.openActions[0].workItemId,
  );
  await page.getByTestId(ids.retroActionPick).nth(1).getByRole('checkbox').check();
  await page.getByTestId(ids.retroActionSave).click();
  await expect(page.getByTestId(ids.retroActionDialog)).toHaveCount(0);
  const writes = stub.writes();
  expect(writes).toHaveLength(1);
  expect(writes[0].path).toBe(
    `/api/retros/actions/${actionDialog.openActions[0].workItemId}/points`,
  );
  expect(writes[0].body).toEqual({ pointIds: [selectedPoint.id, actionDialog.offered[0].id] });
});

test('RetroActionDialog_when opened on an action item starts in Add to existing', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(
    <RetroActionDialogHarness existingWorkItemId={actionDialog.openActions[0].workItemId} />,
  );
  await open(page);
  await expect(page.getByTestId(ids.retroActionExisting)).toHaveValue(
    actionDialog.openActions[0].workItemId,
  );
  await expect(page.getByTestId(ids.retroActionSave)).toHaveText('Add points');
});

test('RetroActionDialog_Save waits for a title, a fix and a point', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await mount(<RetroActionDialogHarness />);
  await open(page);
  const save = page.getByTestId(ids.retroActionSave);
  await expect(save).toBeEnabled();
  await page.getByTestId(ids.retroActionTitle).fill('  ');
  await expect(save).toBeDisabled();
  await page.getByTestId(ids.retroActionTitle).fill('Name the test double');
  await page.getByTestId(ids.retroActionPick).first().getByRole('checkbox').uncheck();
  await expect(save).toBeDisabled();
  await page.getByTestId(ids.retroActionPick).first().getByRole('checkbox').check();
  await save.click();
  await expect(page.getByTestId(ids.retroActionDialog)).toHaveCount(0);
  expect(stub.writes()[0].body).toEqual({
    title: 'Name the test double',
    fixType: 'guide',
    pointIds: [selectedPoint.id],
    themeId: (selectedPoint.theme as NonNullable<RetroPointDto['theme']>).id,
    themeName: null,
  });
});

test('RetroActionDialog_New starts with Guide as the type of fix', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetroActionDialogHarness />);
  await open(page);
  await expect(page.getByRole('radio', { name: /^Guide/ })).toBeChecked();
  await expect(page.getByRole('radio', { name: /^Test/ })).not.toBeChecked();
});

test('RetroActionDialog_type of fix is two columns on a narrow screen and three on a wide one', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<RetroActionDialogHarness />);
  await open(page);
  const columns = () =>
    page
      .getByTestId(ids.retroActionFix)
      .evaluate((el) => getComputedStyle(el).gridTemplateColumns.split(' ').length);
  await page.setViewportSize({ width: 800, height: 700 });
  expect(await columns()).toBe(3);
  await page.setViewportSize({ width: 600, height: 700 });
  expect(await columns()).toBe(2);
});

test('RetroActionDialog_with nothing to offer shows an empty state and a disabled Save', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await serveDialog(page, { ...actionDialog, selected: [], offered: [] });
  await mount(<RetroActionDialogHarness />);
  await open(page);
  await expect(page.getByText('Every point is decided already', { exact: false })).toBeVisible();
  await expect(page.getByTestId(ids.retroActionSave)).toBeDisabled();
});

test('a 409 keeps the dialog and shows a toast', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await page.route(
    (url) => /\/retros\/actions$/.test(url.pathname),
    (route) =>
      route.fulfill({
        status: 409,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'This point is already decided.', code: 'conflict' }),
      }),
  );
  await mount(<RetroActionDialogHarness />);
  await open(page);
  await page.getByTestId(ids.retroActionTitle).fill('My own title');
  await page.getByRole('radio', { name: /^Test/ }).check({ force: true });
  await page.getByTestId(ids.retroActionSave).click();
  await expect(page.getByTestId(componentTestIds.toast)).toContainText(
    'This point is already decided.',
  );
  await expect(page.getByTestId(ids.retroActionDialog)).toBeVisible();
  await expect(page.getByTestId(ids.retroActionTitle)).toHaveValue('My own title');
  await expect.poll(() => stub.reads('/retros/action-dialog').length).toBe(2);
});

test('RetroActionDialog_Escape closes it', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetroActionDialogHarness />);
  await open(page);
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(ids.retroActionDialog)).toHaveCount(0);
});

test('long list keeps Save in view', async ({ mount, page }) => {
  await stubRetros(page);
  const many = Array.from({ length: 100 }, (_, index) => ({
    ...wentBadlyPoint,
    id: `pt-many-${index}`,
    text: `Point number ${index}`,
  }));
  await serveDialog(page, { ...actionDialog, offered: many });
  await mount(<RetroActionDialogHarness />);
  for (const size of [
    { width: 1280, height: 720 },
    { width: 1440, height: 900 },
  ]) {
    await page.setViewportSize(size);
    await open(page);
    await expect(page.getByTestId(ids.retroActionPick)).toHaveCount(101);
    const save = await page.getByTestId(ids.retroActionSave).boundingBox();
    expect(save).not.toBeNull();
    expect(
      (save as { y: number; height: number }).y + (save as { height: number }).height,
    ).toBeLessThanOrEqual(size.height);
    const list = page.getByTestId(ids.retroActionPick).first().locator('..');
    const [scrollHeight, clientHeight] = await list.evaluate((el) => [
      el.scrollHeight,
      el.clientHeight,
    ]);
    expect(scrollHeight).toBeGreaterThan(clientHeight);
    const horizontal = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth);
    expect(horizontal).toBe(false);
    await page.keyboard.press('Escape');
  }
});
