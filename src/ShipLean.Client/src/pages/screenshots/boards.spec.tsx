import { expect } from '@playwright/experimental-ct-react';
import { storiesBoard } from '../../features/board/ct/fixtures';
import { stubBoard } from '../../features/board/ct/stubApi';
import { boardTestIds } from '../../features/board/testIds';
import { boardSettingsTestIds } from '../../features/boardSettings/testIds';
import { settingsDialogTestIds } from '../../features/settingsDialog/testIds';
import {
  BOARD_ID,
  CHECKOUT_FEATURES,
  CHECKOUT_V2_STORIES,
  MOBILE_SIGN_IN_OPEN,
  epicsBoard,
  featuresBoardByEpic,
  featuresBoardNoLanes,
  storiesBoardForSettings,
  storiesSettings,
} from './fixtures/boards';
import { stubShell } from './shellStub';
import { pageShots, type Page } from './shoot';

const LANES = `view:board-lanes:${BOARD_ID}`;
const card = (page: Page, key: string) =>
  page
    .getByTestId(boardTestIds.card)
    .filter({ has: page.getByTestId(boardTestIds.cardKey).filter({ hasText: key }) });
const rollup = (page: Page, key: string, type: string) =>
  card(page, key)
    .getByTestId(boardTestIds.cardRollup)
    .and(page.locator(`[data-type="${type}"]`));

async function stubSettings(page: Page) {
  await stubShell(page);
  await stubBoard(page, storiesBoardForSettings(), {
    members: [],
  });
  await page.route('**/api/projects/p1/tags', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/markdown/render', (route) => {
    const { markdown } = route.request().postDataJSON() as { markdown: string };
    const items = markdown.split('\n').map((line) => `<li>${line.replace(/^- /, '')}</li>`);
    return route.fulfill({ json: { markdown, html: `<ul>${items.join('')}</ul>` } });
  });
  await page.route(`**/api/boards/${BOARD_ID}/settings`, (route) =>
    route.fulfill({ json: storiesSettings() }),
  );
}

pageShots([
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
    name: 'features-board',
    route: '/p/SHIP/boards/features',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubBoard(page, featuresBoardNoLanes(), {
        level: 'features',
        preferences: { [LANES]: 'none' },
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
        children: () => CHECKOUT_V2_STORIES,
      });
    },
    ready: (page) => expect(page.getByTestId(boardTestIds.lane)).toHaveCount(3),
    act: async (page) => {
      await rollup(page, 'SHIP-101', 'story').click();
      await expect(page.getByTestId(boardTestIds.childRow)).toHaveCount(6);
    },
  },
  {
    name: 'features-board-done-confirm',
    route: '/p/SHIP/boards/features',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubBoard(page, featuresBoardNoLanes(true), {
        level: 'features',
        preferences: { [LANES]: 'none' },
        children: () => MOBILE_SIGN_IN_OPEN,
      });
      await page.route('**/api/work-items/*/children', (route) =>
        route.fulfill({ json: MOBILE_SIGN_IN_OPEN }),
      );
    },
    ready: (page) => expect(card(page, 'SHIP-102')).toBeVisible(),
    act: async (page) => {
      await card(page, 'SHIP-102').focus();
      await page.keyboard.press('Alt+ArrowRight');
      await expect(page.getByTestId(boardTestIds.doneConfirm)).toBeVisible();
      await expect(page.getByTestId(boardTestIds.doneConfirmChild)).toHaveCount(4);
    },
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
    act: async (page) => {
      await rollup(page, 'SHIP-90', 'feature').click();
      await expect(page.getByTestId(boardTestIds.childRow)).toHaveCount(3);
    },
  },
  {
    name: 'board-settings',
    route: '/p/SHIP/boards/stories?settings=columns',
    shell: true,
    arrange: stubSettings,
    ready: (page) => expect(page.getByTestId(boardSettingsTestIds.columnRow)).toHaveCount(5),
    act: async (page) => {
      await page.getByTestId(boardSettingsTestIds.columnDodToggle).nth(2).click();
      await expect(page.getByTestId(boardSettingsTestIds.columnDodText)).toBeVisible();
      await expect(page.getByTestId(boardSettingsTestIds.columnDodPreview)).toContainText(
        'Draft PR open and linked',
      );
      await expect(page.getByTestId(settingsDialogTestIds.save)).toBeVisible();
    },
  },
  {
    name: 'board-settings-styles',
    route: '/p/SHIP/boards/stories?settings=styles',
    shell: true,
    arrange: stubSettings,
    ready: (page) => expect(page.getByTestId(boardSettingsTestIds.styleRow)).toHaveCount(3),
    act: async (page) => {
      await expect(page.getByTestId(boardSettingsTestIds.stylesPreview)).toBeVisible();
    },
  },
]);
