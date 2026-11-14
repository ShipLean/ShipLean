// The Feature board on a phone (SHIP-722): one state a page, lanes on every page, and a task's ⋯ sheet. Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/featureBoard.phone.spec.tsx.
import { expect } from '@playwright/experimental-ct-react';
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
]);
