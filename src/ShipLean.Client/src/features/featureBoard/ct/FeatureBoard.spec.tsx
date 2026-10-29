import { expect, test } from '@playwright/experimental-ct-react';
import type { Locator, Page } from 'playwright';
import { pagesTestIds } from '../../pages/testIds';
import { PROJECT } from '../../taskboard/ct/fixtures';
import { taskboardTestIds as ids } from '../../taskboard/testIds';
import { FeatureBoardSlot } from '../../../slots/FeatureBoardSlot';
import { featureBoardTestIds as boardIds } from '../testIds';
import { FEATURE_ID, FEATURE_KEY, FEATURE_VIEW, PARENT_KEY, PARENT_VIEW } from './fixtures';
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

const parentRoute = { hooksConfig: { route: `/p/SHIP/items/${PARENT_KEY}/board` } };
const parentSlot = <FeatureBoardSlot project={PROJECT} itemKey={PARENT_KEY} />;
const people = { saved: { groupBy: 'people' } };

test('FeatureBoard_WhenAParentFeature_ShouldDrawOneSectionPerFeatureInOrderAndIndented', async ({
  mount,
  page,
}) => {
  await stubFeatureBoard(page, PARENT_VIEW);
  await mount(parentSlot, parentRoute);

  const sections = page.getByTestId(boardIds.section);
  await expect(sections).toHaveCount(3);
  await expect(sections.nth(0)).toHaveAttribute('data-section', PARENT_KEY);
  await expect(sections.nth(0).getByTestId(boardIds.sectionTitle)).toHaveText(
    `Directly under ${PARENT_KEY}`,
  );
  await expect(sections.nth(1).getByTestId(boardIds.sectionTitle)).toHaveText('Dashboard widgets');
  await expect(sections.nth(1).getByTestId(boardIds.sectionIn)).toHaveCount(0);
  await expect(sections.nth(2).getByTestId(boardIds.sectionIn)).toHaveText(
    'in SHIP-104 Dashboard widgets',
  );
  await expect(sections.nth(1).getByTestId(boardIds.sectionStories)).toHaveText('0/1 stories');
  await expect(sections.nth(1).getByTestId(boardIds.sectionPoints)).toHaveText('0/5 pts');
  await expect(sections.nth(1).getByTestId(boardIds.sectionOpen)).toHaveAttribute(
    'href',
    /\/items\/SHIP-104$/,
  );
  const indent = (i: number) =>
    sections
      .nth(i)
      .getByTestId(boardIds.sectionHeader)
      .evaluate((el) => parseFloat(getComputedStyle(el).paddingLeft));
  expect(await indent(2)).toBeGreaterThan(await indent(1));
  expect(await indent(1)).toBeGreaterThanOrEqual(await indent(0));
  // The Feature with no stories shows one line, not a grid; each other section has its own column headers.
  await expect(sections.nth(2).getByTestId(boardIds.sectionEmpty)).toHaveText(
    'No stories or bugs yet.',
  );
  await expect(sections.nth(2).getByTestId(ids.grid)).toHaveCount(0);
  await expect(page.getByTestId(ids.grid)).toHaveCount(2);
  await expect(sections.nth(1).getByTestId(ids.columnHeader)).toHaveCount(3);
  await expect(page.getByTestId(boardIds.featuresUnder)).toHaveText('2 Features under it');
});

test('FeatureBoard_WhenTheBoardsFeatureHasNoStoriesOfItsOwn_ShouldLeaveOutItsSection', async ({
  mount,
  page,
}) => {
  const [, widgets] = PARENT_VIEW.rows;
  await stubFeatureBoard(page, { ...PARENT_VIEW, rows: [widgets] });
  await mount(parentSlot, parentRoute);

  const sections = page.getByTestId(boardIds.section);
  await expect(sections).toHaveCount(2);
  await expect(sections.nth(0)).toHaveAttribute('data-section', 'SHIP-104');
  await expect(page.getByTestId(boardIds.jumpChip)).toHaveCount(2);
});

test('FeatureBoard_WhenAJumpChipIsClicked_ShouldScrollToItsSection', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1280, height: 320 });
  await stubFeatureBoard(page, PARENT_VIEW);
  await mount(parentSlot, parentRoute);

  const chips = page.getByTestId(boardIds.jumpChip);
  await expect(chips).toHaveCount(3);
  await expect(chips.nth(1).getByTestId(boardIds.jumpBar)).toHaveAttribute('data-percent', '0');
  const target = page.getByTestId(boardIds.section).nth(2);
  await expect(target).not.toBeInViewport();
  await chips.nth(2).click();
  await expect(target).toBeInViewport();
});

test('FeatureBoard_WhenTheBoardScrolls_ShouldKeepASectionsColumnHeadersAtTheTop', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 200 });
  await stubFeatureBoard(page, PARENT_VIEW);
  await mount(parentSlot, parentRoute);

  const section = page.getByTestId(boardIds.section).nth(1);
  const header = section.getByTestId(ids.columnHeader).first();
  // Scroll the page until the section's own header is out of sight above the column headers.
  const natural = (await header.boundingBox())!.y;
  await page.evaluate((by) => window.scrollBy(0, by), natural + 20);
  await expect(section.getByTestId(boardIds.sectionHeader)).not.toBeInViewport();
  await expect.poll(async () => (await header.boundingBox())!.y).toBeCloseTo(0, 0);
});

test('FeatureBoard_WhenAParentFeature_ShouldNameEachGridAndPointEachToggleAtItsBody', async ({
  mount,
  page,
}) => {
  await stubFeatureBoard(page, PARENT_VIEW);
  await mount(parentSlot, parentRoute);

  const section = page.getByTestId(boardIds.section).nth(1);
  const grid = section.getByTestId(ids.grid);
  await expect(grid).toHaveAttribute('aria-label', /^Feature board: SHIP-103 Reporting \/ /);
  const toggle = section.getByTestId(boardIds.sectionToggle);
  const body = (await toggle.getAttribute('aria-controls'))!;
  await expect(page.locator(`[id="${body}"]`).getByTestId(ids.grid)).toHaveCount(1);
  await toggle.click();
  await expect(toggle).not.toHaveAttribute('aria-controls', /.+/);
});

test('FeatureBoard_WhenASectionIsCollapsed_ShouldHideItsGridAndKeepItsHeader', async ({
  mount,
  page,
}) => {
  await stubFeatureBoard(page, PARENT_VIEW);
  await mount(parentSlot, parentRoute);

  const section = page.getByTestId(boardIds.section).nth(1);
  await expect(section.getByTestId(ids.row)).toHaveCount(1);
  await section.getByTestId(boardIds.sectionToggle).click();
  await expect(section.getByTestId(ids.row)).toHaveCount(0);
  await expect(section.getByTestId(boardIds.sectionHeader)).toBeVisible();
  await expect(page.getByTestId(ids.grid)).toHaveCount(1);
  await section.getByTestId(boardIds.sectionToggle).click();
  await expect(section.getByTestId(ids.row)).toHaveCount(1);
});

test('FeatureBoard_WhenCardDroppedOnAnotherSectionsRow_ShouldPostThatStoryAsTheTarget', async ({
  mount,
  page,
}) => {
  const server = await stubFeatureBoard(page, PARENT_VIEW);
  await mount(parentSlot, parentRoute);

  const sections = page.getByTestId(boardIds.section);
  const from = sections.nth(0).getByTestId(ids.card).first();
  const to = sections.nth(1).getByTestId(ids.cell).nth(1);
  await drag(page, from, to);

  await expect.poll(() => server.featureMoves.length).toBe(1);
  expect(server.featureMoves[0]).toMatchObject({
    stateId: 't-active',
    row: { grouping: 'stories', target: PARENT_VIEW.rows[1].parent.id },
  });
});

test('FeatureBoard_WhenGroupedByPeopleOnAParent_ShouldShowOneGridWithFeatureAndStory', async ({
  mount,
  page,
}) => {
  await stubFeatureBoard(page, PARENT_VIEW, people);
  await mount(parentSlot, parentRoute);

  await expect(page.getByTestId(ids.grid)).toHaveCount(1);
  await expect(page.getByTestId(boardIds.section)).toHaveCount(0);
  const priya = page.getByTestId(ids.row).filter({ hasText: 'Priya Shah' });
  await expect(priya.getByTestId(ids.rowCount)).toHaveText('2 tasks · 2 Features');
  const parents = priya.getByTestId(ids.cardParent);
  await expect(parents).toHaveCount(2);
  await expect(parents.nth(0).getByTestId(ids.cardParentFeature)).toHaveText('Dashboard widgets');
  await expect(parents.nth(1).getByTestId(ids.cardParentFeature)).toHaveText('Reporting');
  await expect(parents.nth(0)).toContainText('Resize widgets');
});

test('FeatureBoard_WhenGroupedByPeopleOnALeaf_ShouldShowTheStoryAloneAndNoFeatureCount', async ({
  mount,
  page,
}) => {
  await stubFeatureBoard(page, FEATURE_VIEW, people);
  await mount(slot, route);

  await expect(page.getByTestId(ids.cardParentFeature)).toHaveCount(0);
  await expect(page.getByTestId(ids.cardParent).first()).toBeVisible();
  await expect(page.getByTestId(ids.rowCount).first()).not.toContainText('Feature');
});
