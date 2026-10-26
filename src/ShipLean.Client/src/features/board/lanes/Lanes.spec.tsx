import { expect, test } from '@playwright/experimental-ct-react';
import { BoardHarness } from '../ct/BoardHarness';
import { featuresBoard, storiesBoard } from '../ct/fixtures';
import { stubBoard } from '../ct/stubApi';
import { boardTestIds as ids } from '../testIds';

const stories = { hooksConfig: { route: '/p/SHIP/boards/stories' } };
const features = { hooksConfig: { route: '/p/SHIP/boards/features' } };
const LANES = 'view:board-lanes:b-stories';
const CARDS = 'view:board-cards:b-stories';

test('Lanes_WhenSwimlanesSwitchedOff_ShouldSaveOffAndRefetchTheBoard', async ({ mount, page }) => {
  const server = await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, stories);
  await expect(page.getByTestId(ids.lanesToggle)).toBeChecked();
  const reads = server.reads.length;

  // click, not check()/uncheck(): the box follows the saved preference, which lands a tick after the click.
  await page.getByTestId(ids.lanesToggle).click();

  await expect.poll(() => server.preferenceWrites).toEqual([{ key: LANES, value: 'off' }]);
  await expect.poll(() => server.reads.length).toBeGreaterThan(reads);
  await expect(page.getByTestId(ids.lanesToggle)).not.toBeChecked();
});

test('Lanes_WhenTheStoriesBoardShowsOneLane_ShouldNotOfferSwimlanes', async ({ mount, page }) => {
  const board = storiesBoard();
  await stubBoard(page, { ...board, lanes: board.lanes.slice(0, 1) });
  await mount(<BoardHarness />, stories);
  await expect(page.getByTestId(ids.toolbar)).toBeVisible();
  await expect(page.getByTestId(ids.lanesToggle)).toHaveCount(0);
});

test('Lanes_WhenOffWasSaved_ShouldOpenWithSwimlanesOffAndSwitchBackOn', async ({ mount, page }) => {
  const server = await stubBoard(page, storiesBoard(), { preferences: { [LANES]: 'off' } });
  await mount(<BoardHarness />, stories);
  await expect(page.getByTestId(ids.lanesToggle)).not.toBeChecked();

  // click, not check()/uncheck(): the box follows the saved preference, which lands a tick after the click.
  await page.getByTestId(ids.lanesToggle).click();

  await expect.poll(() => server.preferenceWrites).toEqual([{ key: LANES, value: 'on' }]);
  await expect(page.getByTestId(ids.lanesToggle)).toBeChecked();
});

test('Lanes_WhenFeaturesByEpicChosen_ShouldSaveEpicAndRefetch', async ({ mount, page }) => {
  const server = await stubBoard(page, featuresBoard(), {
    level: 'features',
    preferences: { 'view:board-lanes:b-stories': 'none' },
  });
  await mount(<BoardHarness level="features" />, features);
  const byEpic = page.getByTestId(ids.lanesOption).filter({ hasText: 'By Epic' });
  await expect(byEpic).toHaveAttribute('aria-pressed', 'false');
  const reads = server.reads.length;

  await byEpic.click();

  await expect(byEpic).toHaveAttribute('aria-pressed', 'true');
  expect(server.preferenceWrites).toEqual([{ key: LANES, value: 'epic' }]);
  await expect.poll(() => server.reads.length).toBeGreaterThan(reads);

  await page.getByTestId(ids.lanesOption).filter({ hasText: 'None' }).click();
  await expect.poll(() => server.preferenceWrites.at(-1)).toEqual({ key: LANES, value: 'none' });
});

test('Lanes_WhenByEpicWasSaved_ShouldOpenByEpic', async ({ mount, page }) => {
  await stubBoard(page, featuresBoard(), {
    level: 'features',
    preferences: { [LANES]: 'epic' },
  });
  await mount(<BoardHarness level="features" />, features);
  await expect(page.getByTestId(ids.lanesOption).filter({ hasText: 'By Epic' })).toHaveAttribute(
    'aria-pressed',
    'true',
  );
});

test('Lanes_WhenOnEpics_ShouldOfferNoLaneChoice', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard({ level: 'epics' }), { level: 'epics' });
  await mount(<BoardHarness level="epics" />, { hooksConfig: { route: '/p/SHIP/boards/epics' } });
  await expect(page.getByTestId(ids.toolbar)).toBeVisible();
  await expect(page.getByTestId(ids.lanesToggle)).toHaveCount(0);
  await expect(page.getByTestId(ids.lanesOption)).toHaveCount(0);
});

test('Cards_WhenCompactChosen_ShouldSaveItPerUser', async ({ mount, page }) => {
  const server = await stubBoard(page, storiesBoard());
  await mount(<BoardHarness />, stories);

  await page.getByTestId(ids.cardsOption).filter({ hasText: 'Compact' }).click();

  await expect(page.getByTestId(ids.card).first()).toHaveAttribute('data-compact', 'true');
  expect(server.preferenceWrites).toEqual([{ key: CARDS, value: 'compact' }]);
});

test('Cards_WhenCompactWasSaved_ShouldOpenCompact', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard(), { preferences: { [CARDS]: 'compact' } });
  await mount(<BoardHarness />, stories);
  await expect(page.getByTestId(ids.card).first()).toHaveAttribute('data-compact', 'true');
});

test('Cards_WhenTheSaveIsRefused_ShouldPutTheChoiceBack', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard());
  await page.route('**/api/me/preferences/*', (route) =>
    route.request().method() === 'PUT'
      ? route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ code: 'internal', error: 'No.' }),
        })
      : route.fulfill({ status: 204 }),
  );
  await mount(<BoardHarness />, stories);

  await page.getByTestId(ids.cardsOption).filter({ hasText: 'Compact' }).click();

  await expect(page.getByTestId(ids.card).first()).toHaveAttribute('data-compact', 'false');
});
