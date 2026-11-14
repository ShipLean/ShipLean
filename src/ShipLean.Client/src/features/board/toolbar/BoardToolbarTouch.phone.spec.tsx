import { expect, test } from '@playwright/experimental-ct-react';
import type { BoardViewDto } from '@shiplean/core/api/views';
import { touchTestIds } from '../../../touch/testIds';
import { BoardHarness } from '../ct/BoardHarness';
import { LocationProbe } from '../ct/LocationProbe';
import { TOM, column, featuresBoard, storiesBoard } from '../ct/fixtures';
import { stubBoard } from '../ct/stubApi';
import { boardTestIds as ids } from '../testIds';

// The board toolbar, Filter, More and ⓘ sheets at 900px and below (SHIP-719, L719b). Runs in the CT phone project.

const stories = { hooksConfig: { route: '/p/SHIP/boards/stories' } };
const features = { hooksConfig: { route: '/p/SHIP/boards/features' } };

// While any filter is set the server hides three cards, and says so on the first column.
function filtered(base: BoardViewDto, search: URLSearchParams): BoardViewDto {
  const on = [...search.keys()].some((name) => name !== 'older');
  return on
    ? {
        ...base,
        columns: base.columns.map((candidate, index) =>
          index === 0 ? column({ ...candidate, hiddenCount: 3 }) : candidate,
        ),
      }
    : base;
}

test('BoardToolbar_WhenTouch_ShouldFitOneRowWithMetaLine', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, stories);
  const meta = page.getByTestId(ids.meta);
  await expect(meta).toContainText('8 open · 30 pts');
  const parts = [
    page.getByTestId(ids.levelSwitch),
    page.getByTestId(ids.filterToggle),
    page.getByTestId(touchTestIds.moreButton),
  ];
  const boxes = [];
  for (const part of parts) boxes.push((await part.boundingBox())!);
  for (const box of boxes) {
    // One row: every control's middle is on the first one's, and fits inside the screen.
    expect(Math.abs(box.y + box.height / 2 - (boxes[0].y + boxes[0].height / 2))).toBeLessThan(2);
    expect(box.x + box.width).toBeLessThanOrEqual(390);
    expect(box.height).toBeGreaterThanOrEqual(32);
  }
  // The meta line is under the row, and nothing from the desktop toolbar is left.
  expect((await meta.boundingBox())!.y).toBeGreaterThan(boxes[0].y + boxes[0].height - 1);
  await expect(page.getByTestId(ids.cardsOption)).toHaveCount(0);
  await expect(page.getByTestId(ids.settingsOpen)).toHaveCount(0);
  await expect(page.getByTestId(ids.filterBar)).toHaveCount(0);
  expect(
    await page.getByTestId(ids.toolbar).evaluate((el) => el.scrollWidth - el.clientWidth),
  ).toBe(0);
  await expect(page.getByTestId(ids.levelOption).nth(2)).toHaveAttribute('aria-current', 'page');
});

test('BoardMoreSheet_WhenCompactPicked_ShouldSwitchCards', async ({ mount, page }) => {
  const server = await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, stories);
  await expect(page.getByTestId(ids.card).first()).toHaveAttribute('data-compact', 'false');

  await page.getByTestId(touchTestIds.moreButton).tap();
  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(ids.cardsOption)).toHaveCount(2);
  await expect(sheet.getByTestId(ids.cardsOption).first()).toHaveAttribute('aria-pressed', 'true');
  await sheet.getByTestId(ids.cardsOption).filter({ hasText: 'Compact' }).tap();

  await expect(sheet).toHaveCount(0);
  await expect(page.getByTestId(ids.card).first()).toHaveAttribute('data-compact', 'true');
  await expect(page.getByTestId(ids.meta)).toContainText('Compact');
  expect(server.preferenceWrites.at(-1)?.value).toBe('compact');
});

test('BoardMoreSheet_WhenFeaturesBoard_ShouldOfferLanesAndPickClosesIt', async ({
  mount,
  page,
}) => {
  const server = await stubBoard(page, featuresBoard(), { level: 'features' });
  await mount(<BoardHarness level="features" />, features);

  await page.getByTestId(touchTestIds.moreButton).tap();
  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(ids.lanesOption)).toHaveText(['None', 'Swimlanes✓', 'By Epic']);
  await sheet.getByTestId(ids.lanesOption).filter({ hasText: 'By Epic' }).tap();

  await expect(sheet).toHaveCount(0);
  expect(server.preferenceWrites.at(-1)?.value).toBe('epic');
});

test('BoardMoreSheet_WhenBoardSettingsPicked_ShouldAskForTheColumnsPane', async ({
  mount,
  page,
}) => {
  await stubBoard(page, storiesBoard());
  await mount(
    <>
      <BoardHarness />
      <LocationProbe />
    </>,
    stories,
  );

  await page.getByTestId(touchTestIds.moreButton).tap();
  await page.getByTestId(touchTestIds.actionSheetRow).filter({ hasText: 'Board settings' }).tap();

  await expect(page.getByTestId(ids.locationProbe)).toHaveText(
    '/p/SHIP/boards/stories?settings=columns',
  );
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
});

test('BoardFilterSheet_WhenOpened_ShouldNotFocusKeyword', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, stories);

  await page.getByTestId(ids.filterToggle).tap();

  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(ids.filterKeyword)).toBeVisible();
  await expect(sheet.getByTestId(ids.filterKeyword)).not.toBeFocused();
  await expect(sheet).toBeFocused();
  expect(
    await sheet.getByTestId(ids.filterKeyword).evaluate((el) => getComputedStyle(el).fontSize),
  ).toBe('16px');
});

test('BoardFilterSheet_WhenPersonPickedAndCleared_ShouldFilterAndRestore', async ({
  mount,
  page,
}) => {
  const server = await stubBoard(page, (search) => filtered(storiesBoard(), search), {
    members: [TOM],
  });
  await mount(<BoardHarness />, stories);
  await expect(page.getByTestId(ids.meta)).not.toContainText('Filtered');

  await page.getByTestId(ids.filterToggle).tap();
  await page.getByTestId(ids.filterPerson).filter({ hasText: 'Tom Okafor' }).tap();

  await expect.poll(() => server.reads.at(-1)).toBe('?assignee=u-tom');
  await expect(page.getByTestId(ids.filterToggle)).toHaveText('Filter (1)');
  await expect(page.getByTestId(ids.meta)).toContainText('Filtered: 11 of 14 cards');

  await page.getByTestId(touchTestIds.sheet).getByTestId(ids.filterClear).tap();

  await expect.poll(() => server.reads.at(-1)).toBe('');
  await expect(page.getByTestId(ids.meta)).not.toContainText('Filtered');
});

test('BoardFilterSheet_WhenShowCardsTappedRightAfterTyping_ShouldKeepTheKeyword', async ({
  mount,
  page,
}) => {
  const server = await stubBoard(page, (search) => filtered(storiesBoard(), search));
  await mount(<BoardHarness />, stories);
  await page.getByTestId(ids.filterToggle).tap();

  await page.getByTestId(ids.filterKeyword).fill('payout');
  await page.getByTestId(ids.filterShow).tap();

  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  await expect.poll(() => server.reads.at(-1)).toBe('?q=payout');
  await expect(page.getByTestId(ids.filterToggle)).toHaveText('Filter (1)');
});

test('BoardFilterSheet_WhenFeaturesBoard_ShouldOfferOwnerAndEpicSelect', async ({
  mount,
  page,
}) => {
  await stubBoard(page, featuresBoard(), {
    level: 'features',
    members: [TOM],
    epics: [{ id: 'e1', title: 'Checkout conversion' }],
  });
  await mount(<BoardHarness level="features" />, features);

  await page.getByTestId(ids.filterToggle).tap();

  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByRole('group', { name: 'Owner' })).toBeVisible();
  await expect(sheet.getByTestId(ids.filterEpic).evaluate((el) => el.tagName)).resolves.toBe(
    'SELECT',
  );
  await expect(sheet.getByTestId(ids.filterSprint)).toHaveCount(0);
});

test('LevelSwitch_WhenFeaturesTapped_ShouldOpenFeaturesBoard', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard());
  await mount(
    <>
      <BoardHarness />
      <LocationProbe />
    </>,
    stories,
  );
  await expect(page.getByTestId(ids.levelOption)).toHaveText(['Epics', 'Features', 'Stories']);

  await page.getByTestId(ids.levelOption).filter({ hasText: 'Features' }).tap();

  await expect(page.getByTestId(ids.locationProbe)).toHaveText('/p/SHIP/boards/features');
});

test('DodSheet_WhenInfoTapped_ShouldOpenSheetNotPopover', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, stories);

  await page.getByTestId(ids.dodOpen).first().tap();

  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(touchTestIds.sheetTitle)).toHaveText('New');
  await expect(sheet.getByTestId(ids.dodType).first()).toBeVisible();
  await expect(page.getByTestId(ids.dod)).toHaveCount(0);
  await sheet.getByTestId(touchTestIds.sheetClose).tap();
  await expect(sheet).toHaveCount(0);
});
