import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import { componentTestIds } from '../../components/testIds';
import { rollupBoard } from '../../features/board/ct/fixtures';
import { stubBoard } from '../../features/board/ct/stubApi';
import { boardTestIds as ids } from '../../features/board/testIds';
import { touchTestIds } from '../../touch/testIds';
import { BOARD_ID, CHECKOUT_V2_STORIES, featuresBoardByEpic } from './fixtures/boards';
import {
  boardWithExpediteInReady,
  boardWithPointsHidden,
  boardWithTwoNew,
} from './fixtures/boardDetails';
import { holdCardOver } from './fixtures/holdCardOver';
import { stubShell } from './shellStub';
import { phonePageShots } from './shootPhone';
import type { Page } from './shoot';

// SHIP-1828 (BD3) on a phone, as whole pages at 390px: what's under a Feature listed inside its card (BACKLOG-18), the
// split-column captions (BACKLOG-24), lanes by Epic with the Epic's icon and key (BACKLOG-26), the outline on the cell a
// held card would land in (BACKLOG-21), and the card ⋯ at the right edge with points hidden. Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/boardDetails.phone.spec.tsx.

const LANES = `view:board-lanes:${BOARD_ID}`;
const stories = { hooksConfig: { route: '/p/SHIP/boards/stories' } };
const features = { hooksConfig: { route: '/p/SHIP/boards/features' } };

const card = (page: Page, key: string) =>
  page
    .getByTestId(ids.card)
    .filter({ has: page.getByTestId(ids.cardKey).filter({ hasText: key }) });

const turnTo = (page: Page, columnId: string) =>
  page
    .getByTestId(touchTestIds.pagerChip)
    .and(page.locator(`[data-page-id="${columnId}"]`))
    .tap();

const openStoriesOf = (page: Page, key: string) =>
  card(page, key).locator(`[data-testid="${ids.cardRollup}"][data-type="story"]`).tap();

phonePageShots([
  {
    name: 'features-board-children-inline',
    route: '/p/SHIP/boards/features',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubBoard(page, rollupBoard(), {
        level: 'features',
        children: () => CHECKOUT_V2_STORIES,
      });
    },
    ready: (page) => expect(card(page, 'SHIP-101')).toBeVisible(),
    act: async (page) => {
      await openStoriesOf(page, 'SHIP-101');
      await expect(page.getByTestId(ids.childRow)).toHaveCount(CHECKOUT_V2_STORIES.length);
    },
  },
  {
    name: 'board-drag-outline',
    route: '/p/SHIP/boards/stories',
    shell: true,
    arrange: async (page) => {
      await page.clock.install({ time: 0 });
      await stubShell(page);
      await stubBoard(page, boardWithTwoNew());
    },
    ready: (page) => expect(card(page, 'SHIP-163')).toBeVisible(),
    act: (page) => holdCardOver(page, 'SHIP-164', 'SHIP-163'),
  },
  {
    name: 'board-points-hidden',
    route: '/p/SHIP/boards/stories',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubBoard(page, boardWithPointsHidden());
    },
    ready: (page) => expect(card(page, 'SHIP-160')).toBeVisible(),
  },
]);

// BACKLOG-18: at touch the roll-up opens the list inside the card (no floating panel), 32px rows with a state dot, then
// the ways to add a child; the same roll-up closes it.
test('FeatureCard_AtTouch_ShouldListChildrenInsideTheCard', async ({ mount, page }) => {
  await stubShell(page);
  await stubBoard(page, rollupBoard(), { level: 'features', children: () => CHECKOUT_V2_STORIES });
  await mount(<AppRoutes />, features);
  const feature = card(page, 'SHIP-101');
  const rollup = feature.locator(`[data-testid="${ids.cardRollup}"][data-type="story"]`);
  await expect(rollup).toHaveAttribute('aria-expanded', 'false');

  await rollup.tap();
  const list = feature.getByTestId(ids.childList);
  await expect(list).toBeVisible();
  await expect(page.getByTestId(ids.childList)).toHaveCount(1);
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(rollup).toHaveAttribute('aria-expanded', 'true');
  const rows = list.getByTestId(ids.childRow);
  await expect(rows).toHaveCount(CHECKOUT_V2_STORIES.length);
  // Inside the card's box, under its roll-ups.
  const cardBox = (await feature.boundingBox())!;
  const listBox = (await list.boundingBox())!;
  const rollupBox = (await rollup.boundingBox())!;
  expect(listBox.x).toBeGreaterThanOrEqual(cardBox.x);
  expect(listBox.x + listBox.width).toBeLessThanOrEqual(cardBox.x + cardBox.width);
  expect(listBox.y).toBeGreaterThanOrEqual(rollupBox.y + rollupBox.height);
  expect(listBox.y + listBox.height).toBeLessThanOrEqual(cardBox.y + cardBox.height);
  // Each row is 32px and shows its state with the coloured dot.
  for (let i = 0; i < CHECKOUT_V2_STORIES.length; i += 1) {
    expect((await rows.nth(i).boundingBox())!.height).toBeGreaterThanOrEqual(32);
    await expect(rows.nth(i).getByTestId(componentTestIds.stateDot)).toHaveAttribute(
      'data-category',
      CHECKOUT_V2_STORIES[i].state.category,
    );
  }
  await expect(list.getByTestId(ids.childAdd).first()).toHaveText('+ User story');
  await expect(list.getByTestId(ids.childAdd).nth(1)).toHaveText('+ Bug');
  // A tap on a row's state is the list's, not the card's: the item does not open.
  await rows.first().getByTestId(ids.childState).tap();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(list).toBeVisible();

  await rollup.tap();
  await expect(page.getByTestId(ids.childList)).toHaveCount(0);
});

// BACKLOG-21: while a held card is over a cell, that cell is outlined in the accent colour; the others are not.
test('BoardDrag_WhileOverACell_ShouldOutlineIt', async ({ mount, page }) => {
  await page.clock.install({ time: 0 });
  await stubShell(page);
  await stubBoard(page, boardWithTwoNew());
  await mount(<AppRoutes />, stories);
  const target = page
    .getByTestId(ids.cell)
    .and(page.locator('[data-lane="lane-default"][data-column="col-new"]'));
  await expect(target).toHaveCSS('outline-style', 'none');

  await holdCardOver(page, 'SHIP-164', 'SHIP-163');
  await expect(target).toHaveCSS('outline-style', 'solid');
  await expect(target).toHaveCSS('outline-width', '2px');
  const accent = await target.evaluate((node) =>
    getComputedStyle(node).getPropertyValue('--color-accent').trim(),
  );
  expect(accent).not.toBe('');
  const outlined = await page
    .getByTestId(ids.cell)
    .evaluateAll(
      (cells) => cells.filter((node) => getComputedStyle(node).outlineStyle === 'solid').length,
    );
  expect(outlined).toBe(1);
});

// BACKLOG-21: over the end of another cell (below its last card, or in an empty one) that cell is outlined, and the cell the
// card was lifted from is not.
test('BoardDrag_WhileOverTheEndOfAnotherCell_ShouldOutlineThatCellOnly', async ({
  mount,
  page,
}) => {
  await page.clock.install({ time: 0 });
  await stubShell(page);
  await stubBoard(page, boardWithTwoNew());
  await mount(<AppRoutes />, stories);
  const cellOf = (laneId: string) =>
    page.getByTestId(ids.cell).and(page.locator(`[data-lane="${laneId}"][data-column="col-new"]`));
  const origin = cellOf('lane-default');
  const target = cellOf('lane-expedite');

  await holdCardOver(page, 'SHIP-164', { cell: target });

  await expect(target).toHaveCSS('outline-style', 'solid');
  await expect(origin).toHaveCSS('outline-style', 'none');
});

// BACKLOG-24: a split column's halves say what they hold.
test('SplitColumn_ShouldShowCaptions', async ({ mount, page }) => {
  await stubShell(page);
  await stubBoard(page, boardWithExpediteInReady());
  await mount(<AppRoutes />, stories);
  await turnTo(page, 'col-ready');
  const lane = page
    .getByTestId(touchTestIds.pagerPage)
    .and(page.locator('[data-page-id="col-ready"]'))
    .getByTestId(ids.columnLane)
    .and(page.locator('[data-lane="lane-expedite"]'));
  await expect(lane.getByTestId(ids.columnLaneSub)).toHaveText([
    /^Doing\s*1\s*being worked on$/,
    /^Done\s*0\s*ready to pull$/,
  ]);
});

// BACKLOG-26: a lane by Epic carries the Epic's type icon and key in its header.
test('LanesByEpic_ShouldShowEpicIconAndKey', async ({ mount, page }) => {
  await stubShell(page);
  await stubBoard(page, featuresBoardByEpic(), {
    level: 'features',
    preferences: { [LANES]: 'epic' },
  });
  await mount(<AppRoutes />, features);
  const header = page
    .getByTestId(ids.columnLane)
    .and(page.locator('[data-lane="lane-SHIP-90"]'))
    .first()
    .getByTestId(ids.columnLaneHeader);
  await expect(header.getByTestId(componentTestIds.typeIcon)).toHaveAttribute('data-type', 'epic');
  await expect(header).toContainText('Checkout conversion');
  await expect(header).toContainText('SHIP-90');
});
