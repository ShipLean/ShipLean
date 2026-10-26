import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { ToastHost } from '../../../components/ToastHost';
import { componentTestIds } from '../../../components/testIds';
import { backlogView, itemRow, PROJECT } from '../ct/fixtures';
import { pressUntil, settled, slidUp } from '../ct/keyboardDrag';
import { stubBacklog } from '../ct/stubApi';
import { backlogTestIds as ids } from '../testIds';
import { ListMode } from '../list/ListMode';

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };
const ROWS = [
  itemRow({ key: 'SHIP-1', rank: 1, revision: 4 }),
  itemRow({ key: 'SHIP-2', rank: 2, revision: 5 }),
  itemRow({ key: 'SHIP-3', rank: 3, revision: 6 }),
];
const keys = (page: {
  getByTestId(id: string): { evaluateAll<T>(f: (e: Element[]) => T): Promise<T> };
}) => page.getByTestId(ids.row).evaluateAll((els) => els.map((e) => e.getAttribute('data-key')));

// Picks the first row up by its grip and puts it down on the third row with the mouse. The button is released only once
// dnd-kit has drawn the drop: the rows between slide up out of the way, which it does when it knows the target.
async function dragFirstOntoThird(page: Page) {
  const grip = (await page.getByTestId(ids.grip).first().boundingBox())!;
  const target = (await page.getByTestId(ids.row).nth(2).boundingBox())!;
  await page.mouse.move(grip.x + grip.width / 2, grip.y + grip.height / 2);
  await page.mouse.down();
  await page.mouse.move(grip.x + 5, grip.y + 20, { steps: 4 });
  await page.mouse.move(grip.x + 5, target.y + target.height / 2 + 6, { steps: 10 });
  await expect(page.getByTestId(ids.row).nth(2)).toHaveAttribute(
    'style',
    /translate3d\(0px, -[1-9]/,
  );
  await page.mouse.up();
}

test('Drag_WhenRowDropped_ShouldPostReorderWithRevisionAndBeforeItem', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);

  await dragFirstOntoThird(page);

  await expect.poll(() => server.reorders.length).toBe(1);
  expect(server.reorders[0].url).toContain('/api/work-items/id-SHIP-1/reorder');
  expect(server.reorders[0].body).toEqual({ expectedRevision: 4, beforeItemId: null });
});

test('Drag_WhenDroppedBackInPlace_ShouldSendNothing', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await page.getByTestId(ids.grip).nth(1).focus();
  await page.keyboard.press('Space');
  await expect(page.getByTestId(ids.grip).nth(1)).toHaveAttribute('aria-pressed', 'true');
  // The sensor takes a key once the row below has slid up; ArrowUp then puts the row back where it was.
  await pressUntil(page, 'ArrowDown', slidUp(page.getByTestId(ids.row).nth(2)));
  await page.keyboard.press('ArrowUp');
  await settled(page.getByTestId(ids.row).nth(2))();
  await page.keyboard.press('Space');
  await expect(page.getByTestId(ids.grip).nth(1)).not.toHaveAttribute('aria-pressed', 'true');
  expect(server.reorders).toHaveLength(0);
  expect(await keys(page)).toEqual(['SHIP-1', 'SHIP-2', 'SHIP-3']);
});

test('Drag_WhenMovedWithTheKeyboard_ShouldPostReorder', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await page.getByTestId(ids.grip).first().focus();
  await page.keyboard.press('Space');
  await expect(page.getByTestId(ids.grip).first()).toHaveAttribute('aria-pressed', 'true');
  // dnd-kit has taken the key when the row below has slid up out of the way.
  await pressUntil(page, 'ArrowDown', slidUp(page.getByTestId(ids.row).nth(1)));
  await page.keyboard.press('Space');

  await expect.poll(() => server.reorders.length).toBe(1);
  expect(server.reorders[0].url).toContain('/api/work-items/id-SHIP-1/reorder');
  expect(server.reorders[0].body).toEqual({ expectedRevision: 4, beforeItemId: 'id-SHIP-3' });
});

test('Drag_WhenDropped_ShouldToastTheMove', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(
    <>
      <ListMode project={PROJECT} />
      <ToastHost />
    </>,
    route,
  );
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await dragFirstOntoThird(page);
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('SHIP-1 reordered');
});

const staleBody = {
  error: 'This item changed.',
  code: 'conflict',
  current: { id: 'id-SHIP-1', revision: 9, changedBy: null },
  theirChanges: [{ rev: 9, changedBy: { name: 'Tom Okafor' } }],
};

test('Drag_WhenReorderIsStale_ShouldOfferUseMineThatResendsAgainstTheNewRevision', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, backlogView(ROWS), {
    reorderAnswers: [
      { status: 409, body: staleBody },
      {
        status: 200,
        body: { id: 'id-SHIP-1', key: 'SHIP-1', revision: 10, changes: [], notices: [] },
      },
    ],
  });
  await mount(<ListMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await dragFirstOntoThird(page);

  await expect(page.getByTestId(componentTestIds.conflictBanner)).toContainText('Tom Okafor');
  await page.getByTestId(componentTestIds.conflictUseMine).click();

  await expect(page.getByTestId(componentTestIds.conflictBanner)).toHaveCount(0);
  expect(server.reorders).toHaveLength(2);
  expect(server.reorders[1].body).toEqual({ expectedRevision: 9, beforeItemId: null });
});

test('Drag_WhenReorderIsStale_ShouldKeepTheirsByReadingTheBacklogAgain', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, backlogView(ROWS), {
    reorderAnswers: [{ status: 409, body: staleBody }],
  });
  await mount(<ListMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(3);
  await dragFirstOntoThird(page);
  await expect(page.getByTestId(componentTestIds.conflictBanner)).toBeVisible();
  const readsBefore = server.reads.length;

  await page.getByTestId(componentTestIds.conflictKeepTheirs).click();

  await expect(page.getByTestId(componentTestIds.conflictBanner)).toHaveCount(0);
  await expect.poll(() => server.reads.length).toBeGreaterThan(readsBefore);
  expect(server.reorders).toHaveLength(1);
  expect(await keys(page)).toEqual(['SHIP-1', 'SHIP-2', 'SHIP-3']);
});
