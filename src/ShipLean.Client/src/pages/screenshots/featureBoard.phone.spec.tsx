// The Feature board on a phone (SHIP-722): one state a page, lanes on every page, and a task's ⋯ sheet. Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/featureBoard.phone.spec.tsx.
import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import { featureBoardTestIds } from '../../features/featureBoard/testIds';
import { taskboardTestIds } from '../../features/taskboard/testIds';
import { touchTestIds } from '../../touch/testIds';
import { FEATURE_BOARD_PARENT_VIEW, FEATURE_BOARD_VIEW } from './fixtures/featureBoardView';
import {
  FEATURE_BOARD_PARENT_ROUTE,
  FEATURE_BOARD_PEOPLE,
  FEATURE_BOARD_ROUTE,
  arrangeFeatureBoard,
  featureBoardReady,
} from './fixtures/featureBoardStubs';
import type { Page } from './shoot';
import { phonePageShots } from './shootPhone';

const chip = (page: Page, name: string) =>
  page
    .getByTestId(touchTestIds.pagerChip)
    .filter({ hasText: new RegExp(`^\\s*${name}(?![A-Za-z])`) });

phonePageShots([
  {
    name: 'feature-board',
    route: FEATURE_BOARD_ROUTE,
    shell: true,
    arrange: (p) => arrangeFeatureBoard(p, FEATURE_BOARD_VIEW),
    ready: featureBoardReady,
  },
  {
    name: 'feature-board-active',
    route: FEATURE_BOARD_ROUTE,
    shell: true,
    arrange: (p) => arrangeFeatureBoard(p, FEATURE_BOARD_VIEW),
    ready: featureBoardReady,
    act: async (page) => {
      await chip(page, 'Active').tap();
      await expect(chip(page, 'Active')).toHaveAttribute('aria-current', 'true');
      // The smooth scroll has settled once the Active page's header is wholly on screen.
      await expect(
        page
          .getByTestId(taskboardTestIds.phonePageHead)
          .filter({ hasText: /^\s*Active/ })
          .first(),
      ).toBeInViewport({ ratio: 1 });
    },
  },
  {
    name: 'feature-board-parent',
    route: FEATURE_BOARD_PARENT_ROUTE,
    shell: true,
    arrange: (p) => arrangeFeatureBoard(p, FEATURE_BOARD_PARENT_VIEW),
    ready: featureBoardReady,
  },
  {
    name: 'feature-board-people',
    route: FEATURE_BOARD_PARENT_ROUTE,
    shell: true,
    arrange: (p) => arrangeFeatureBoard(p, FEATURE_BOARD_PARENT_VIEW, FEATURE_BOARD_PEOPLE),
    ready: featureBoardReady,
  },
  {
    name: 'feature-board-task-actions',
    route: FEATURE_BOARD_ROUTE,
    shell: true,
    arrange: (p) => arrangeFeatureBoard(p, FEATURE_BOARD_VIEW),
    ready: featureBoardReady,
    act: async (page) => {
      await page.getByTestId(taskboardTestIds.cardActions).first().tap();
      await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
    },
  },
  {
    name: 'feature-board-add',
    route: FEATURE_BOARD_ROUTE,
    shell: true,
    arrange: (p) => arrangeFeatureBoard(p, FEATURE_BOARD_VIEW),
    ready: featureBoardReady,
    act: async (page) => {
      await page.getByTestId(taskboardTestIds.addStory).tap();
      await expect(page.getByTestId(taskboardTestIds.addStoryBox)).toBeVisible();
    },
  },
  {
    name: 'feature-board-assign',
    route: FEATURE_BOARD_ROUTE,
    shell: true,
    arrange: (p) => arrangeFeatureBoard(p, FEATURE_BOARD_VIEW),
    ready: featureBoardReady,
    act: async (page) => {
      await page.getByTestId(taskboardTestIds.cardActions).first().tap();
      await page.locator('[data-action-id="assignee"]').tap();
      await expect(page.getByTestId(taskboardTestIds.assigneeOption).first()).toBeVisible();
    },
  },
  {
    // Backlog order: the closed line SHIP-138 sits second, between open lanes (TBOARD-1), and the last line is a closed one too.
    name: 'feature-board-end',
    route: FEATURE_BOARD_ROUTE,
    shell: true,
    arrange: (p) => arrangeFeatureBoard(p, FEATURE_BOARD_VIEW),
    ready: featureBoardReady,
    act: async (page) => {
      const entries = page
        .getByTestId(touchTestIds.pagerPage)
        .first()
        .locator(
          `[data-testid="${taskboardTestIds.lane}"],[data-testid="${taskboardTestIds.closedLine}"]`,
        );
      await expect(entries.nth(1)).toHaveAttribute('data-testid', taskboardTestIds.closedLine);
      await expect(entries.last()).toHaveAttribute('data-testid', taskboardTestIds.closedLine);
      await entries
        .last()
        .evaluate((el) =>
          el.scrollIntoView({ block: 'end', inline: 'nearest', behavior: 'instant' }),
        );
      await expect(entries.last()).toBeInViewport({ ratio: 0.9 });
    },
  },
  {
    name: 'feature-board-filtered',
    route: FEATURE_BOARD_ROUTE,
    shell: true,
    arrange: (p) => arrangeFeatureBoard(p, FEATURE_BOARD_VIEW),
    ready: featureBoardReady,
    act: async (page) => {
      await page.getByTestId(taskboardTestIds.filterButton).tap();
      await page.getByTestId(taskboardTestIds.filterSheetRow).nth(1).tap();
      await expect(page.getByTestId(taskboardTestIds.filterButton)).toContainText('Filter1');
      await page.getByTestId(touchTestIds.sheetClose).tap();
      await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
      await expect(page.getByTestId(taskboardTestIds.laneHidden).first()).toBeVisible();
    },
  },
  {
    name: 'feature-board-parent-section',
    route: FEATURE_BOARD_PARENT_ROUTE,
    shell: true,
    arrange: (p) => arrangeFeatureBoard(p, FEATURE_BOARD_PARENT_VIEW),
    ready: featureBoardReady,
    act: async (page) => {
      const band = page
        .getByTestId(featureBoardTestIds.band)
        .filter({
          has: page.getByTestId(featureBoardTestIds.sectionKey).filter({ hasText: 'SHIP-104' }),
        })
        .first();
      await band.evaluate((el) =>
        el.scrollIntoView({ block: 'start', inline: 'nearest', behavior: 'instant' }),
      );
      await expect(band).toBeInViewport({ ratio: 0.9 });
    },
  },
]);

// SHIP-1626 TBOARD-2 (K5): at 390px the second page peeks 30-36px at the right edge.
test('TouchPager_OnTheFeatureBoardPage_ShouldPeekTheNextPage', async ({ mount, page }) => {
  await arrangeFeatureBoard(page, FEATURE_BOARD_VIEW);
  await mount(<AppRoutes />, { hooksConfig: { route: FEATURE_BOARD_ROUTE } });
  await featureBoardReady(page);
  const first = (await page.getByTestId(touchTestIds.pagerPage).nth(0).boundingBox())!;
  const second = (await page.getByTestId(touchTestIds.pagerPage).nth(1).boundingBox())!;
  const track = (await page.getByTestId(touchTestIds.pager).boundingBox())!;
  // The design's page: 340px from x=12, whatever the page's own gutter.
  expect(first.x).toBe(12);
  expect(first.width).toBe(340);
  // K5: the next page peeks 30-36px at 390px, on screen and inside the track (the pager bleeds to the screen edges).
  const peek = Math.min(390, track.x + track.width) - second.x;
  expect(peek).toBeGreaterThanOrEqual(30);
  expect(peek).toBeLessThanOrEqual(36);
  // And nothing clips it: 15px in from the screen edge is the second page itself.
  const secondId = await page
    .getByTestId(touchTestIds.pagerPage)
    .nth(1)
    .getAttribute('data-page-id');
  const hit = await page.evaluate(
    ({ x, y, pageTestId }) =>
      document
        .elementFromPoint(x, y)
        ?.closest(`[data-testid="${pageTestId}"]`)
        ?.getAttribute('data-page-id'),
    { x: 375, y: second.y + 10, pageTestId: touchTestIds.pagerPage },
  );
  expect(hit).toBe(secondId);
  // The bleed reaches the screen edges and no further: the page never scrolls sideways.
  const sideways = await page.evaluate(
    () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
  );
  expect(sideways).toBe(false);
});
