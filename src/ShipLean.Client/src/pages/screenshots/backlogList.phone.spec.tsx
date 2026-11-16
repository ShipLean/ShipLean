import { expect } from '@playwright/experimental-ct-react';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { backlogView, itemRow, review, ROWS, TOM } from '../../features/backlog/ct/fixtures';
import { stubBacklog } from '../../features/backlog/ct/stubApi';
import { backlogTestIds } from '../../features/backlog/testIds';
import { touchTestIds } from '../../touch/testIds';
import { phonePageShots } from './shootPhone';
import { stubShell } from './shellStub';
import type { Page } from './shoot';

// The Stories backlog's List mode on a phone (SHIP-716 D1): two-line touch rows in place of the table, the same page with
// the Not-estimated check on, the inline add row open, and a row held mid-drag.

const route = '/p/SHIP/backlogs/stories';
const SPRINT = { id: 'sp24', name: 'Sprint 24', status: 'current' } as const;
const FEATURE = {
  id: 'f1',
  key: 'SHIP-90',
  type: { id: 'feature', name: 'Feature' },
  title: 'Mobile sign-in',
} as const;

// Twelve rows to read a Stories list by: ranks down the screen, three of them unestimated.
const MANY_ROWS: BacklogRowDto[] = [
  [158, 'Fix the double charge on retry', 5, 'bug'],
  [160, 'Biometric sign-in', null, 'story'],
  [161, 'Saved cards on the payment step', 8, 'story'],
  [162, 'Receipt email shows the wrong currency', 3, 'bug'],
  [163, 'Guest checkout without an account', null, 'story'],
  [164, 'Apple Pay button on the cart', 5, 'story'],
  [165, 'Order history filters', null, 'story'],
  [166, 'Retry a declined card', 2, 'story'],
  [167, 'Promo code field loses focus', 1, 'bug'],
  [168, 'Delivery estimate on the cart', 8, 'story'],
  [169, 'Address autocomplete', 13, 'story'],
  [170, 'Save the basket between visits', 3, 'story'],
].map(([n, title, points, type], i) =>
  itemRow({
    key: `SHIP-${n}`,
    rank: i + 1,
    title: title as string,
    points: points as number | null,
    type: type === 'bug' ? { id: 'bug', name: 'Bug' } : { id: 'story', name: 'User Story' },
    assignee: i % 3 === 0 ? TOM : null,
    sprint: i < 3 ? SPRINT : null,
    parent: i % 2 === 0 ? FEATURE : null,
  }),
);

const UNESTIMATED = MANY_ROWS.filter((row) => row.points === null).map((row) => row.id);

const arrange = async (page: Page) => {
  await stubShell(page);
  await stubBacklog(page, backlogView(ROWS));
};

const arrangeMany = async (page: Page) => {
  await stubShell(page);
  await stubBacklog(page, {
    ...backlogView(MANY_ROWS),
    review: review({ unestimated: UNESTIMATED }),
  });
};

interface Point {
  x: number;
  y: number;
}

// Picks the row up by its handle (CDP touch, the clock moved by hand) and holds it over a lower row: the shot is taken
// while the finger is still down.
async function holdRowOver(page: Page, from: string, over: string) {
  const cdp = await page.context().newCDPSession(page);
  const send = (type: 'touchStart' | 'touchMove', point: Point) =>
    cdp.send('Input.dispatchTouchEvent', { type, touchPoints: [point] });
  const rowOf = (key: string) =>
    page.locator(`[data-testid="${backlogTestIds.row}"][data-key="${key}"]`);
  const handle = (await rowOf(from).getByTestId(backlogTestIds.touchHandle).boundingBox())!;
  const target = (await rowOf(over).boundingBox())!;
  const start = {
    x: Math.round(handle.x + handle.width / 2),
    y: Math.round(handle.y + handle.height / 2),
  };
  const end = { x: start.x + 150, y: Math.round(target.y + target.height / 2) };

  await send('touchStart', start);
  await page.clock.runFor(500);
  const steps = Math.max(1, Math.ceil((end.y - start.y) / 20));
  for (let i = 1; i <= steps; i++) {
    await send('touchMove', {
      x: Math.round(start.x + ((end.x - start.x) * i) / steps),
      y: Math.round(start.y + ((end.y - start.y) * i) / steps),
    });
  }
  await expect(page.getByTestId(touchTestIds.dragGhost)).toBeVisible();
}

phonePageShots([
  {
    name: 'backlogs-rows',
    route,
    shell: true,
    arrange,
    ready: (page) => expect(page.getByTestId(backlogTestIds.row)).toHaveCount(ROWS.length),
  },
  {
    name: 'backlogs',
    route,
    shell: true,
    arrange: arrangeMany,
    ready: (page) => expect(page.getByTestId(backlogTestIds.row)).toHaveCount(MANY_ROWS.length),
  },
  {
    name: 'backlogs-add',
    route,
    shell: true,
    arrange,
    ready: (page) => expect(page.getByTestId(backlogTestIds.row)).toHaveCount(ROWS.length),
    act: async (page) => {
      await page.getByTestId(backlogTestIds.newItem).tap();
      await page.getByTestId(backlogTestIds.addTitle).fill('Remember address');
      await expect(page.getByTestId(backlogTestIds.addTitle)).toHaveValue('Remember address');
    },
  },
  {
    name: 'backlogs-drag-list',
    route,
    shell: true,
    arrange: async (page) => {
      await page.clock.install({ time: 0 });
      await arrangeMany(page);
    },
    ready: (page) => expect(page.getByTestId(backlogTestIds.row)).toHaveCount(MANY_ROWS.length),
    act: (page) => holdRowOver(page, 'SHIP-160', 'SHIP-164'),
  },
]);
