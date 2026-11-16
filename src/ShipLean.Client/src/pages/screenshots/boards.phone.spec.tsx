import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../AppRoutes';
import { card as boardCard, cell, lane, storiesBoard } from '../../features/board/ct/fixtures';
import { stubBoard } from '../../features/board/ct/stubApi';
import { cardsPreferenceName } from '../../features/board/lanes/boardPreferences';
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

const FULL_VIEWPORT = { ratio: 1 } as const;

// The stories board with two cards in the default lane's In progress cell, so one can be held over the other.
function boardWithTwoInProgress() {
  const base = storiesBoard();
  return {
    ...base,
    lanes: base.lanes.map((candidate) =>
      candidate.id !== 'lane-default'
        ? candidate
        : lane({
            ...candidate,
            cells: candidate.cells.map((c, i) =>
              i === 3
                ? cell('col-prog', [
                    boardCard({
                      key: 'SHIP-163',
                      title: 'Saved cards on the payment step',
                      points: 5,
                    }),
                    boardCard({
                      key: 'SHIP-164',
                      title: 'Receipt email shows the wrong currency',
                      points: 3,
                    }),
                  ])
                : c,
            ),
          }),
    ),
  };
}

// storiesBoard() with an Expedite card in Ready's Doing half, so the split column shows two lanes.
function boardWithExpediteInReady() {
  const base = storiesBoard();
  return {
    ...base,
    lanes: base.lanes.map((candidate) =>
      candidate.id !== 'lane-expedite'
        ? candidate
        : lane({
            ...candidate,
            cardCount: 2,
            cells: candidate.cells.map((c, i) =>
              i === 1
                ? cell('col-ready', [
                    boardCard({
                      key: 'SHIP-151',
                      title: 'Fix payout rounding on refunds',
                      points: 3,
                    }),
                  ])
                : c,
            ),
          }),
    ),
  };
}

interface Point {
  x: number;
  y: number;
}

// Turns to In progress, long-presses `from` (CDP touch, the clock moved by hand) and moves it over `over`. The shot is
// taken while the finger is still down. The caller installs the clock in `arrange`.
async function holdCardOver(page: Page, from: string, over: string) {
  await page
    .getByTestId(touchTestIds.pagerChip)
    .and(page.locator('[data-page-id="col-prog"]'))
    .tap();
  await expect(card(page, from)).toBeInViewport(FULL_VIEWPORT);
  await expect(card(page, over)).toBeInViewport(FULL_VIEWPORT);
  const cdp = await page.context().newCDPSession(page);
  const send = (type: 'touchStart' | 'touchMove', point: Point) =>
    cdp.send('Input.dispatchTouchEvent', { type, touchPoints: [point] });
  const centre = async (key: string): Promise<Point> => {
    const box = (await card(page, key).boundingBox())!;
    return { x: Math.round(box.x + box.width / 2), y: Math.round(box.y + box.height / 2) };
  };
  const start = await centre(from);
  const end = await centre(over);

  await send('touchStart', start);
  await page.clock.runFor(450);
  await expect(page.getByTestId(touchTestIds.dragGhost)).toBeVisible();
  const steps = Math.max(1, Math.ceil(Math.hypot(end.x - start.x, end.y - start.y) / 20));
  for (let i = 1; i <= steps; i++) {
    await send('touchMove', {
      x: Math.round(start.x + ((end.x - start.x) * i) / steps),
      y: Math.round(start.y + ((end.y - start.y) * i) / steps),
    });
  }
  await expect(page.getByTestId(boardTestIds.dropLine)).toHaveCount(1);
}

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
      await stubBoard(page, boardWithExpediteInReady());
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
  {
    name: 'board-compact',
    route: '/p/SHIP/boards/stories',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubBoard(page, storiesBoard(), {
        preferences: { [cardsPreferenceName(BOARD_ID)]: 'compact' },
      });
    },
    ready: (page) => expect(page.getByTestId(boardTestIds.card).first()).toBeVisible(),
  },
  {
    name: 'board-drag',
    route: '/p/SHIP/boards/stories',
    shell: true,
    arrange: async (page) => {
      await page.clock.install({ time: 0 });
      await stubShell(page);
      await stubBoard(page, boardWithTwoInProgress());
    },
    ready: (page) => expect(page.getByTestId(boardTestIds.card).first()).toBeVisible(),
    act: (page) => holdCardOver(page, 'SHIP-164', 'SHIP-163'),
  },
  {
    name: 'features-board-drag',
    route: '/p/SHIP/boards/features',
    shell: true,
    arrange: async (page) => {
      await page.clock.install({ time: 0 });
      await stubShell(page);
      await stubBoard(page, featuresBoardSwimlanes(), {
        level: 'features',
        preferences: { [LANES]: 'on' },
      });
    },
    ready: (page) => expect(card(page, 'SHIP-113')).toBeVisible(),
    act: (page) => holdCardOver(page, 'SHIP-102', 'SHIP-101'),
  },
]);

// SHIP-1626 TBOARD-2 (K5): at 390px the second page peeks 30-36px at the right edge.
test('TouchPager_OnTheStoriesBoardPage_ShouldPeekTheNextPage', async ({ mount, page }) => {
  await stubShell(page);
  await stubBoard(page, storiesBoard());
  await mount(<AppRoutes />, { hooksConfig: { route: '/p/SHIP/boards/stories' } });
  await expect(page.getByTestId(boardTestIds.card).first()).toBeVisible();
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
  const secondId = await page.getByTestId(touchTestIds.pagerPage).nth(1).getAttribute('data-page-id');
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
