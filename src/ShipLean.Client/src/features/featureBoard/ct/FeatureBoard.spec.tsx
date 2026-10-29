import { expect, test } from '@playwright/experimental-ct-react';
import type { Locator, Page } from 'playwright';
import { pagesTestIds } from '../../pages/testIds';
import { PROJECT } from '../../taskboard/ct/fixtures';
import { taskboardTestIds as ids } from '../../taskboard/testIds';
import { FeatureBoardSlot } from '../../../slots/FeatureBoardSlot';
import { featureBoardTestIds as boardIds } from '../testIds';
import { FEATURE_ID, FEATURE_KEY, FEATURE_VIEW } from './fixtures';
import { stubFeatureBoard } from './stubFeatureBoard';

const route = { hooksConfig: { route: `/p/SHIP/items/${FEATURE_KEY}/board` } };
const slot = <FeatureBoardSlot project={PROJECT} itemKey={FEATURE_KEY} />;

// Picks the card up with the pointer and puts it down on the middle of a cell.
async function drag(page: Page, from: Locator, to: Locator) {
  await to.scrollIntoViewIfNeeded();
  await expect(from).toBeVisible();
  await expect(to).toBeVisible();
  const a = (await from.boundingBox())!;
  const b = (await to.boundingBox())!;
  const startX = a.x + a.width / 2;
  const startY = a.y + a.height / 2;
  await page.mouse.move(startX, startY);
  await page.mouse.down();
  await page.mouse.move(startX + 6, startY + 6, { steps: 4 });
  await page.mouse.move(b.x + b.width / 2, b.y + b.height - 8, { steps: 12 });
  await page.mouse.up();
}

test('FeatureBoard_WhenLoaded_ShouldShowTheFeatureAndItsRollUp', async ({ mount, page }) => {
  const server = await stubFeatureBoard(page, FEATURE_VIEW);
  await mount(slot, route);

  await expect(page.getByTestId(boardIds.key)).toHaveText(FEATURE_KEY);
  await expect(page.getByTestId(boardIds.title)).toHaveText('Checkout v2');
  await expect(page.getByTestId(boardIds.state)).toHaveText('Active');
  await expect(page.getByTestId(boardIds.featuresUnder)).toHaveCount(0);
  await expect(page.getByTestId(boardIds.stories)).toHaveText('1 of 2 stories and bugs closed');
  await expect(page.getByTestId(boardIds.points)).toHaveText('0/5 pts');
  await expect(page.getByTestId(boardIds.pointsBar)).toHaveAttribute('data-percent', '0');
  await expect(page.getByTestId(boardIds.tasks)).toHaveText('Tasks 2/4');
  await expect(page.getByTestId(boardIds.back)).toHaveText(`← ${FEATURE_KEY} Checkout v2`);
  expect(server.featureReads).toEqual([FEATURE_KEY]);
});

test('FeatureBoard_WhenLoaded_ShouldListTheSprintsItSpansAndBacklog', async ({ mount, page }) => {
  await stubFeatureBoard(page, FEATURE_VIEW);
  await mount(slot, route);

  const pills = page.getByTestId(boardIds.sprints).getByTestId(ids.rowSprint);
  await expect(pills).toHaveText(['◷ Sprint 24', '◷ Sprint 25', 'Backlog']);
  await expect(pills.nth(0)).toHaveAttribute('data-sprint', 'current');
  await expect(pills.nth(1)).toHaveAttribute('data-sprint', 'future');
  await expect(pills.nth(2)).toHaveAttribute('data-sprint', 'backlog');
});

test('FeatureBoard_WhenLoaded_ShouldShowEachRowsSprintAndNoUnparentedRow', async ({
  mount,
  page,
}) => {
  await stubFeatureBoard(page, FEATURE_VIEW);
  await mount(slot, route);

  const rows = page.getByTestId(ids.row);
  await expect(rows).toHaveCount(2);
  await expect(rows.nth(0).getByTestId(ids.rowSprint)).toHaveText('◷ Sprint 24');
  await expect(rows.nth(0).getByTestId(ids.rowSprint)).toHaveAttribute('data-sprint', 'current');
  await expect(rows.nth(0).getByTestId(ids.card)).toHaveCount(3);
  // The closed story starts collapsed, so it shows no pill (its sprint is in the title's tooltip on the design).
  await expect(rows.nth(1)).toHaveAttribute('data-collapsed', 'true');
  await rows.nth(1).getByTestId(ids.rowToggle).click();
  await expect(rows.nth(1).getByTestId(ids.rowSprint)).toHaveText('Backlog');
  await expect(page.locator('[data-row="unparented"]')).toHaveCount(0);
  await expect(page.getByTestId(ids.columnHeader)).toHaveCount(3);
});

test('FeatureBoard_WhenAddingAStoryOrBug_ShouldCreateItUnderTheFeatureWithNoSprint', async ({
  mount,
  page,
}) => {
  const server = await stubFeatureBoard(page, FEATURE_VIEW);
  await mount(slot, route);

  await page.getByTestId(ids.addStory).click();
  await page.getByTestId(ids.addStoryTitle).fill('Guest checkout');
  await page.getByTestId(ids.addStoryTitle).press('Enter');

  await expect(page.getByTestId(ids.addStoryTitle)).toHaveValue('');
  expect(server.creates).toHaveLength(1);
  expect(server.creates[0]).toMatchObject({
    type: 'story',
    title: 'Guest checkout',
    parentId: FEATURE_ID,
    sprintId: null,
  });
});

test('FeatureBoard_WhenCardDropped_ShouldPostToTheFeatureBoardMoves', async ({ mount, page }) => {
  const server = await stubFeatureBoard(page, FEATURE_VIEW);
  await mount(slot, route);

  const from = page.getByTestId(ids.row).nth(0).getByTestId(ids.card).first();
  const to = page.getByTestId(ids.row).nth(0).getByTestId(ids.cell).nth(1);
  await drag(page, from, to);

  await expect.poll(() => server.featureMoves.length).toBe(1);
  expect(server.featureMoves[0]).toMatchObject({ stateId: 't-active' });
  expect(server.moves).toHaveLength(0);
});

test('FeatureBoard_WhenNotAFeature_ShouldShowNotFound', async ({ mount, page }) => {
  await stubFeatureBoard(page, FEATURE_VIEW, { knownKey: 'SHIP-999' });
  await mount(slot, route);

  await expect(page.getByTestId(pagesTestIds.notFound)).toBeVisible();
  await expect(page.getByTestId(pagesTestIds.slotFeatureBoard)).toHaveCount(0);
});
