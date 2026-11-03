import { expect, test } from '@playwright/experimental-ct-react';
import { sprintRetro, stubRetros } from '../../retros/ct/retroFixtures';
import { retroSharedTestIds as ids } from '../testIds';
import type { AddTarget } from './AddTarget';
import { RetroBoardHarness } from './ct/RetroBoardHarness';

const item = sprintRetro.coverage[0].item;
const sprintAdd: AddTarget = {
  kind: 'sprint',
  sprintId: 'sprint-24',
  about: [
    { value: 'sprint-24', label: 'Sprint' },
    { value: item.id, label: `${item.key} ${item.title}` },
  ],
};
const column = (category: string) =>
  `[data-testid="${ids.retroBoardColumn}"][data-category="${category}"]`;

test('AddNote_WhenSavedInASprint_ShouldPostTheSprintByDefaultAndNeverARetroId', async ({
  mount,
  page,
}) => {
  const stub = await stubRetros(page);
  await mount(<RetroBoardHarness add={sprintAdd} />);
  await page.locator(column('improve')).getByTestId(ids.retroBoardAdd).click();
  await expect(page.getByTestId(ids.retroBoardAddAbout)).toHaveValue('sprint-24');
  await page.getByTestId(ids.retroBoardAddText).fill('Pair on the first slice');
  await page.getByTestId(ids.retroBoardAddSave).click();

  await expect.poll(() => stub.writes().length).toBe(1);
  const write = stub.writes()[0];
  expect(write.method).toBe('POST');
  expect(write.path).toBe('/api/projects/p1/retros/points');
  expect(write.body).toEqual({
    retroId: null,
    workItemId: null,
    sprintId: 'sprint-24',
    category: 'improve',
    text: 'Pair on the first slice',
    rootCause: null,
    themeId: null,
    themeName: null,
  });
  await expect(page.getByTestId(ids.retroBoardAddText)).toHaveCount(0);
});

test('AddNote_WhenAnItemIsChosenAboutAndWentBadly_ShouldPostTheWorkItemAndRootCause', async ({
  mount,
  page,
}) => {
  const stub = await stubRetros(page);
  await mount(<RetroBoardHarness add={sprintAdd} />);
  await page.locator(column('wentBadly')).getByTestId(ids.retroBoardAdd).click();
  const values = await page
    .getByTestId(ids.retroBoardAddAbout)
    .locator('option')
    .evaluateAll((els) => els.map((e) => (e as HTMLOptionElement).value));
  expect(new Set(values).size).toBe(values.length);
  await page.getByTestId(ids.retroBoardAddAbout).selectOption(item.id);
  await page.getByTestId(ids.retroBoardAddText).fill('The migration was slow');
  await page.getByTestId(ids.retroBoardAddCause).fill('No index on the join');
  await page.getByTestId(ids.retroBoardAddSave).click();

  await expect.poll(() => stub.writes().length).toBe(1);
  expect(stub.writes()[0].body).toEqual({
    retroId: null,
    workItemId: item.id,
    sprintId: null,
    category: 'wentBadly',
    text: 'The migration was slow',
    rootCause: 'No index on the join',
    themeId: null,
    themeName: null,
  });
});

test('AddNote_WhenOnAnItemsRetro_ShouldPostThatWorkItemWithNoAboutChoice', async ({
  mount,
  page,
}) => {
  const stub = await stubRetros(page);
  await mount(<RetroBoardHarness add={{ kind: 'item', workItemId: 'w1' }} />);
  await page.locator(column('wentWell')).getByTestId(ids.retroBoardAdd).click();
  await expect(page.getByTestId(ids.retroBoardAddAbout)).toHaveCount(0);
  await page.getByTestId(ids.retroBoardAddText).fill('Clean handover');
  await page.getByTestId(ids.retroBoardAddSave).click();
  await expect.poll(() => stub.writes().length).toBe(1);
  expect(stub.writes()[0].body).toMatchObject({
    retroId: null,
    workItemId: 'w1',
    sprintId: null,
  });
});

test('AddNote_WhenNotWentBadly_ShouldOfferNoRootCauseAndSendNull', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await mount(<RetroBoardHarness add={sprintAdd} />);
  await page.locator(column('improve')).getByTestId(ids.retroBoardAdd).click();
  await expect(page.getByTestId(ids.retroBoardAddCause)).toHaveCount(0);
  await page.getByTestId(ids.retroBoardAddCancel).click();

  await page.locator(column('wentBadly')).getByTestId(ids.retroBoardAdd).click();
  await expect(page.getByTestId(ids.retroBoardAddCause)).toBeVisible();
  await page.getByTestId(ids.retroBoardAddCause).fill('   ');
  await page.getByTestId(ids.retroBoardAddText).fill('Flaky run');
  await page.getByTestId(ids.retroBoardAddSave).click();
  await expect.poll(() => stub.writes().length).toBe(1);
  expect(stub.writes()[0].body).toMatchObject({ category: 'wentBadly', rootCause: null });
});

test('AddNote_WhenTheTextIsBlank_ShouldNotAllowSaving', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await mount(<RetroBoardHarness add={sprintAdd} />);
  await page.locator(column('improve')).getByTestId(ids.retroBoardAdd).click();
  await expect(page.getByTestId(ids.retroBoardAddSave)).toBeDisabled();
  await page.getByTestId(ids.retroBoardAddText).fill('   ');
  await expect(page.getByTestId(ids.retroBoardAddSave)).toBeDisabled();
  await page.getByTestId(ids.retroBoardAddText).fill('Something');
  await expect(page.getByTestId(ids.retroBoardAddSave)).toBeEnabled();
  expect(stub.writes()).toHaveLength(0);
});

test('AddNote_WhenTheServerRejects_ShouldKeepTheTextAndShowAToast', async ({ mount, page }) => {
  await stubRetros(page);
  await page.route(
    (url) => url.pathname === '/api/projects/p1/retros/points',
    (route) =>
      route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Text is too long.', code: 'validation', field: 'text' }),
      }),
  );
  await mount(<RetroBoardHarness add={sprintAdd} />);
  await page.locator(column('improve')).getByTestId(ids.retroBoardAdd).click();
  await page.getByTestId(ids.retroBoardAddText).fill('x'.repeat(5));
  await page.getByTestId(ids.retroBoardAddSave).click();

  await expect(page.getByRole('region', { name: 'Notifications' })).toContainText(
    'Text is too long.',
  );
  await expect(page.getByTestId(ids.retroBoardAddText)).toHaveValue('xxxxx');
});

test('AddNote_WhenASecondEditorOpens_ShouldCloseTheFirst', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetroBoardHarness add={sprintAdd} />);
  await page.locator(column('improve')).getByTestId(ids.retroBoardAdd).click();
  await page.getByTestId(ids.retroBoardAddText).fill('draft');
  await page.locator(column('wentWell')).getByTestId(ids.retroBoardAdd).click();

  await expect(page.getByTestId(ids.retroBoardAddText)).toHaveCount(1);
  await expect(page.locator(column('wentWell')).getByTestId(ids.retroBoardAddText)).toBeVisible();
  await expect(page.locator(column('improve')).getByTestId(ids.retroBoardAdd)).toBeVisible();
});

test('AddNote_WhenCancelled_ShouldPutTheAddButtonBack', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetroBoardHarness add={sprintAdd} />);
  await page.locator(column('improve')).getByTestId(ids.retroBoardAdd).click();
  await page.getByTestId(ids.retroBoardAddCancel).click();
  await expect(page.getByTestId(ids.retroBoardAddText)).toHaveCount(0);
  await expect(page.getByTestId(ids.retroBoardAdd)).toHaveCount(4);
});
