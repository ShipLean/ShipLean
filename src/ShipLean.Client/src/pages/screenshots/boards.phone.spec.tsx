import { expect } from '@playwright/experimental-ct-react';
import { storiesBoard } from '../../features/board/ct/fixtures';
import { stubBoard } from '../../features/board/ct/stubApi';
import { boardTestIds } from '../../features/board/testIds';
import { touchTestIds } from '../../touch/testIds';
import {
  BOARD_ID,
  CHECKOUT_FEATURES,
  epicsBoard,
  featuresBoardByEpic,
  featuresBoardSwimlanes,
} from './fixtures/boards';
import { stubShell } from './shellStub';
import { phonePageShots } from './shootPhone';
import type { Page } from './shoot';

// The Stories, Features and Epics boards as whole pages on a phone (SHIP-719, L719d). Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/boards.phone.spec.tsx.

const LANES = `view:board-lanes:${BOARD_ID}`;
const card = (page: Page, key: string) =>
  page
    .getByTestId(boardTestIds.card)
    .filter({ has: page.getByTestId(boardTestIds.cardKey).filter({ hasText: key }) });

phonePageShots([
  {
    name: 'board',
    route: '/p/SHIP/boards/stories',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubBoard(page, storiesBoard());
    },
    ready: (page) => expect(page.getByTestId(boardTestIds.card).first()).toBeVisible(),
  },
  {
    name: 'board-ready',
    route: '/p/SHIP/boards/stories',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubBoard(page, storiesBoard());
    },
    ready: (page) => expect(page.getByTestId(boardTestIds.card).first()).toBeVisible(),
    act: async (page) => {
      await page
        .getByTestId(touchTestIds.pagerChip)
        .and(page.locator('[data-page-id="col-ready"]'))
        .tap();
      await expect(card(page, 'SHIP-161')).toBeInViewport({ ratio: 1 });
    },
  },
  {
    name: 'features-board',
    route: '/p/SHIP/boards/features',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubBoard(page, featuresBoardSwimlanes(), {
        level: 'features',
        preferences: { [LANES]: 'on' },
      });
    },
    ready: (page) => expect(card(page, 'SHIP-113')).toBeVisible(),
  },
  {
    name: 'features-board-lanes',
    route: '/p/SHIP/boards/features',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubBoard(page, featuresBoardByEpic(), {
        level: 'features',
        preferences: { [LANES]: 'epic' },
      });
    },
    ready: (page) => expect(page.getByTestId(boardTestIds.columnLane).first()).toBeVisible(),
  },
  {
    name: 'epics-board',
    route: '/p/SHIP/boards/epics',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubBoard(page, epicsBoard(), { level: 'epics', children: () => CHECKOUT_FEATURES });
    },
    ready: (page) => expect(card(page, 'SHIP-89')).toBeVisible(),
  },
]);
