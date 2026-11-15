// The Feature board on a phone (SHIP-722): one state a page, lanes on every page, and a task's ⋯ sheet. Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/featureBoard.phone.spec.tsx.
import { expect } from '@playwright/experimental-ct-react';
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
    // Closed stories come after every open lane today (TBOARD-1): this shoots that order, not the designed one.
    name: 'feature-board-end',
    route: FEATURE_BOARD_ROUTE,
    shell: true,
    arrange: (p) => arrangeFeatureBoard(p, FEATURE_BOARD_VIEW),
    ready: featureBoardReady,
    act: async (page) => {
      const last = page
        .getByTestId(touchTestIds.pagerPage)
        .first()
        .getByTestId(taskboardTestIds.closedLine)
        .last();
      await last.evaluate((el) =>
        el.scrollIntoView({ block: 'end', inline: 'nearest', behavior: 'instant' }),
      );
      await expect(last).toBeInViewport({ ratio: 0.9 });
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
      await expect(page.getByTestId(taskboardTestIds.filterButton)).toHaveText('Filter (1)');
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
