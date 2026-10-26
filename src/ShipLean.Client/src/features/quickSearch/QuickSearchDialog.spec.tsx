import { expect, test, type Page } from '@playwright/experimental-ct-react';
import { quickSearchDialogHarnessIds as harness } from './ct/harnessIds';
import { QuickSearchDialogHarness } from './ct/QuickSearchDialogHarness';
import { quickSearchTestIds as ids } from './testIds';
import type { QuickSearchGroup, QuickSearchResults } from './types';

const nothing = (): void => undefined;

const itemsGroup: QuickSearchGroup = {
  id: 'items',
  title: 'Work items',
  rows: [
    {
      kind: 'item',
      id: 'SHIP-41',
      item: {
        key: 'SHIP-41',
        title: 'Proxy sign-in for installs',
        type: 'feature',
        state: 'Active',
        stateCategory: 'inProgress',
        parentPath: ['SHIP-12'],
        projectKey: 'SHIP',
      },
      match: null,
    },
    {
      kind: 'item',
      id: 'SHIP-97',
      item: {
        key: 'SHIP-97',
        title: 'Sprint review export',
        type: 'story',
        state: 'Active',
        stateCategory: 'inProgress',
        parentPath: ['SHIP-1', 'SHIP-2', 'SHIP-30'],
        projectKey: 'SHIP',
      },
      match: { in: 'comment', snippet: 'The export needs a sign-off first' },
    },
    {
      kind: 'item',
      id: 'SHIP-130',
      item: {
        key: 'SHIP-130',
        title: 'Paste images',
        type: 'story',
        state: 'New',
        stateCategory: 'proposed',
        parentPath: [],
        projectKey: 'SHIP',
      },
      match: { in: 'attachment', name: 'design-signoff.png', snippet: 'design-signoff.png' },
    },
  ],
};

const sprintsGroup: QuickSearchGroup = {
  id: 'sprints',
  title: 'Sprints',
  rows: [
    {
      kind: 'sprint',
      id: '/p/SHIP/sprints/24',
      name: 'Sprint 24',
      goal: 'Sign-in hardening',
      datesLabel: '14 – 27 Oct',
      current: true,
      projectKey: 'SHIP',
      open: nothing,
    },
  ],
};

const projectsGroup: QuickSearchGroup = {
  id: 'projects',
  title: 'Projects',
  rows: [
    {
      kind: 'project',
      id: 'DS',
      key: 'DS',
      name: 'Digital signage',
      detail: '2 members',
      current: false,
      open: nothing,
    },
  ],
};

const actionsGroup: QuickSearchGroup = {
  id: 'actions',
  title: 'Actions',
  rows: [
    {
      kind: 'action',
      id: 'create-story',
      glyph: '+',
      label: 'Create story',
      detail: 'in SHIP',
      run: nothing,
    },
  ],
};

function results(over: Partial<QuickSearchResults> = {}): QuickSearchResults {
  return {
    groups: [itemsGroup, sprintsGroup, projectsGroup, actionsGroup],
    status: 'ready',
    note: 'Top 8 items · keep typing to narrow',
    retry: null,
    ...over,
  };
}

const highlightedKey = (page: Page) =>
  page.locator(`[data-testid="${ids.option}"][data-highlighted="true"]`);

test('Dialog_WhenOpened_ShouldFocusTheInputAsACombobox', async ({ mount, page }) => {
  await mount(<QuickSearchDialogHarness results={results()} />);
  const input = page.getByTestId(ids.input);
  await expect(page.getByTestId(ids.dialog)).toHaveAttribute('aria-modal', 'true');
  await expect(page.getByTestId(ids.dialog)).toHaveAttribute('aria-label', 'Quick search');
  await expect(input).toBeFocused();
  await expect(input).toHaveAttribute('role', 'combobox');
  await expect(input).toHaveAttribute('aria-autocomplete', 'list');
  const listId = await page.getByTestId(ids.list).getAttribute('id');
  await expect(input).toHaveAttribute('aria-controls', listId ?? '');
  const firstId = await page.getByTestId(ids.option).first().getAttribute('id');
  await expect(input).toHaveAttribute('aria-activedescendant', firstId ?? '');
  await expect(page.getByTestId(ids.group)).toHaveText([
    'Work items',
    'Sprints',
    'Projects',
    'Actions',
  ]);

  // Focus stays in the input when the list is pressed.
  await page.getByTestId(ids.option).nth(1).hover();
  await page.mouse.down();
  await expect(input).toBeFocused();
  await page.mouse.up();
});

test('Dialog_WhenOpenedWithAQuery_ShouldSelectIt', async ({ mount, page }) => {
  await mount(<QuickSearchDialogHarness results={results()} initialQuery="sign" />);
  const selection = await page
    .getByTestId(ids.input)
    .evaluate((input: HTMLInputElement) => [input.selectionStart, input.selectionEnd]);
  expect(selection).toEqual([0, 4]);
  await page.keyboard.type('x');
  await expect(page.getByTestId(harness.query)).toHaveText('x');
});

test('Dialog_WhenArrowKeysPressed_ShouldMoveAndWrapOverRowsSkippingHeaders', async ({
  mount,
  page,
}) => {
  await mount(<QuickSearchDialogHarness results={results()} />);
  const options = page.getByTestId(ids.option);
  await expect(options).toHaveCount(6);
  await expect(options.first()).toHaveAttribute('data-highlighted', 'true');

  // Up from the first row wraps to the last, which is the action after three headers.
  await page.keyboard.press('ArrowUp');
  await expect(highlightedKey(page)).toHaveAttribute('data-id', 'create-story');
  await page.keyboard.press('ArrowDown');
  await expect(highlightedKey(page)).toHaveAttribute('data-key', 'SHIP-41');
  for (let step = 0; step < 3; step += 1) await page.keyboard.press('ArrowDown');
  await expect(highlightedKey(page)).toHaveAttribute('data-id', '/p/SHIP/sprints/24');
  await expect(page.getByTestId(ids.input)).toHaveAttribute(
    'aria-activedescendant',
    (await highlightedKey(page).getAttribute('id')) ?? '',
  );
  await expect(highlightedKey(page)).toHaveCount(1);
});

test('Dialog_WhenEnterOnAnItem_ShouldOpenItAndFinish', async ({ mount, page }) => {
  await mount(<QuickSearchDialogHarness results={results()} />);
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Enter');
  await expect(page.getByTestId(harness.opened)).toHaveText('SHIP-97');
  await expect(page.getByTestId(harness.done)).toHaveText('1');
  await expect(page.getByTestId(harness.ran)).toHaveText('');
});

test('Dialog_WhenEnterOnASprint_ShouldRunItsOpenAndFinish', async ({ mount, page }) => {
  await mount(<QuickSearchDialogHarness results={results()} />);
  await page.getByTestId(ids.option).and(page.locator('[data-kind="sprint"]')).hover();
  await expect(highlightedKey(page)).toHaveAttribute('data-kind', 'sprint');
  await page.keyboard.press('Enter');
  await expect(page.getByTestId(harness.ran)).toHaveText('/p/SHIP/sprints/24');
  await expect(page.getByTestId(harness.done)).toHaveText('1');
  await expect(page.getByTestId(harness.opened)).toHaveText('');
});

test('Dialog_WhenAnActionClicked_ShouldRunItAndFinish', async ({ mount, page }) => {
  await mount(<QuickSearchDialogHarness results={results()} />);
  await page.locator('[data-id="create-story"]').click();
  await expect(page.getByTestId(harness.ran)).toHaveText('create-story');
  await expect(page.getByTestId(harness.done)).toHaveText('1');
});

test('Dialog_WhenEscapePressed_ShouldClose', async ({ mount, page }) => {
  await mount(<QuickSearchDialogHarness results={results()} />);
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(harness.closed)).toHaveText('1');
});

test('Dialog_WhenScrimClicked_ShouldClose', async ({ mount, page }) => {
  await mount(<QuickSearchDialogHarness results={results()} />);
  await page.getByTestId(ids.dialog).click();
  await expect(page.getByTestId(harness.closed)).toHaveText('0');
  await page.getByTestId(ids.scrim).click({ position: { x: 4, y: 4 } });
  await expect(page.getByTestId(harness.closed)).toHaveText('1');
});

test('Dialog_WhenQueryChanges_ShouldHighlightTheFirstRow', async ({ mount, page }) => {
  await mount(<QuickSearchDialogHarness results={results()} initialQuery="a" />);
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('ArrowDown');
  await expect(highlightedKey(page)).toHaveAttribute('data-key', 'SHIP-130');
  await page.getByTestId(ids.input).press('End');
  await page.keyboard.type('b');
  await expect(highlightedKey(page)).toHaveAttribute('data-key', 'SHIP-41');
});

test('Dialog_WhenMatchedInAComment_ShouldShowWhereItMatched', async ({ mount, page }) => {
  await mount(<QuickSearchDialogHarness results={results()} initialQuery="sign" />);
  const where = page.getByTestId(ids.where).first();
  await expect(where).toContainText('In a comment');
  await expect(where).toContainText('The export needs a sign-off first');
  await expect(where.getByTestId(ids.match)).toHaveText('sign');
  // The title is marked on a row that matched there, and the parent path is shortened past two levels.
  await expect(page.getByTestId(ids.option).first().getByTestId(ids.match)).toHaveText('sign');
  await expect(page.getByTestId(ids.option).nth(1)).toContainText('… › SHIP-2 › SHIP-30');
});

test('Dialog_WhenMatchedInAnAttachment_ShouldNameTheAttachment', async ({ mount, page }) => {
  await mount(<QuickSearchDialogHarness results={results()} initialQuery="sign" />);
  const where = page.getByTestId(ids.where).nth(1);
  await expect(where).toContainText('In attachment design-signoff.png');
  await expect(where.getByTestId(ids.match)).toHaveText('sign');
});

test('Dialog_WhenLoading_ShouldShowTheBarAndKeepTheRows', async ({ mount, page }) => {
  await mount(<QuickSearchDialogHarness results={results({ status: 'loading' })} />);
  await expect(page.getByTestId(ids.loading)).toBeVisible();
  await expect(page.getByTestId(ids.option)).toHaveCount(6);
  // Dimmed rows can still be picked.
  await page.locator('[data-key="SHIP-41"]').click();
  await expect(page.getByTestId(harness.opened)).toHaveText('SHIP-41');
});

test('Dialog_WhenError_ShouldOfferRetryAndKeepTheActions', async ({ mount, page }) => {
  await mount(
    <QuickSearchDialogHarness
      results={results({ groups: [actionsGroup], status: 'error', retry: nothing })}
      initialQuery="sign"
    />,
  );
  await expect(page.getByTestId(ids.error)).toBeVisible();
  await expect(page.getByTestId(ids.retry)).toBeVisible();
  await expect(page.getByTestId(ids.option)).toHaveCount(1);
  await expect(page.getByTestId(ids.option)).toHaveAttribute('data-id', 'create-story');
  // An error is not "nothing matches".
  await expect(page.getByTestId(ids.none)).toHaveCount(0);
});

test('Dialog_WhenNothingMatches_ShouldSayWhatWasSearched', async ({ mount, page }) => {
  await mount(
    <QuickSearchDialogHarness results={results({ groups: [actionsGroup] })} initialQuery="zzz" />,
  );
  await expect(page.getByTestId(ids.none)).toContainText('Nothing matches “zzz”');
  await expect(page.getByTestId(ids.none)).toContainText(
    'Searched titles, descriptions, comments and attachments in your projects.',
  );
  await expect(page.getByTestId(ids.option)).toHaveCount(1);
});

test('Dialog_WhenQueryEmptyAndNoRows_ShouldNotSayNothingMatches', async ({ mount, page }) => {
  await mount(<QuickSearchDialogHarness results={results({ groups: [] })} />);
  await expect(page.getByTestId(ids.none)).toHaveCount(0);
});

test('Dialog_WhenNarrow_ShouldHideTheKeyHints', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1200, height: 800 });
  await mount(<QuickSearchDialogHarness results={results()} />);
  await expect(page.getByTestId(ids.footer)).toContainText('move');
  await expect(page.getByTestId(ids.note)).toHaveText('Top 8 items · keep typing to narrow');
  await page.setViewportSize({ width: 600, height: 800 });
  await expect(page.getByTestId(ids.footer).getByText('move')).toBeHidden();
  await expect(page.getByTestId(ids.note)).toBeVisible();
});
