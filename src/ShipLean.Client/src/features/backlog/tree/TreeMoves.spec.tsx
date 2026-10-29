import { expect, test } from '@playwright/experimental-ct-react';
import { ToastHost } from '../../../components/ToastHost';
import { componentTestIds } from '../../../components/testIds';
import { PROJECT, treeRows, treeView } from '../ct/fixtures';
import { pressUntil } from '../ct/keyboardDrag';
import { stubBacklog } from '../ct/stubApi';
import { backlogTestIds as ids } from '../testIds';
import { TreeMode } from './TreeMode';

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=tree' } };
const { rows } = treeRows();
const view = treeView(rows);
const idOf = (key: string) => rows.find((r) => r.key === key)!.id;

interface Box {
  x: number;
  y: number;
  width: number;
  height: number;
}
interface Locatable {
  boundingBox(): Promise<Box | null>;
}
interface TreePage {
  mouse: {
    move(x: number, y: number, options?: { steps: number }): Promise<void>;
    down(): Promise<void>;
    up(): Promise<void>;
  };
  getByTestId(id: string): { filter(options: { hasText: string }): { first(): Locatable } };
}

const sequence = (page: {
  getByTestId(id: string): { evaluate<T>(f: (e: Element) => T): Promise<T> };
}) =>
  page
    .getByTestId(ids.list)
    .evaluate((el) =>
      [...el.querySelectorAll('tbody tr')].map((tr) => tr.getAttribute('data-key') ?? ''),
    );

async function dragTo(page: TreePage, key: string, to: Box) {
  const grip = (await page.getByTestId(ids.row).filter({ hasText: key }).first().boundingBox())!;
  const startX = grip.x + 12;
  const startY = grip.y + grip.height / 2;
  await page.mouse.move(startX, startY);
  await page.mouse.down();
  await page.mouse.move(startX + 6, startY + 10, { steps: 4 });
  await page.mouse.move(to.x + to.width / 2, to.y + to.height / 2, { steps: 12 });
  await page.mouse.up();
}

const dragOnto = async (page: TreePage, key: string, target: string) =>
  dragTo(
    page,
    key,
    (await page.getByTestId(ids.row).filter({ hasText: target }).first().boundingBox())!,
  );

const dragOntoGroup = async (page: TreePage, key: string, groupText: string) =>
  dragTo(
    page,
    key,
    (await page.getByTestId(ids.treeGroup).filter({ hasText: groupText }).first().boundingBox())!,
  );

const stale = {
  error: 'Changed by someone else',
  code: 'conflict',
  current: { revision: 99, changedBy: null },
  theirChanges: [],
};

test('Tree_WhenDroppedTwiceBeforeTheBacklogIsReadAgain_ShouldSendTheRevisionTheServerReturned', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, view);
  await mount(
    <>
      <TreeMode project={PROJECT} />
      <ToastHost />
    </>,
    route,
  );
  await expect(page.getByTestId(ids.row)).toHaveCount(6);

  // The server holds SHIP-141 at 4 and bumps it with each parent change; a PATCH at any other revision is a 409.
  let revision = 4;
  await page.route('**/api/work-items/*', async (r) => {
    const body = r.request().postDataJSON() as { expectedRevision: number };
    server.patches.push({ url: r.request().url(), body });
    if (body.expectedRevision !== revision) {
      await r.fulfill({
        status: 409,
        contentType: 'application/json',
        body: JSON.stringify(stale),
      });
      return;
    }
    revision += 1;
    await r.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ id: 'x', key: 'x', revision, changes: [], notices: [] }),
    });
  });
  // The backlog read that follows each drop is held, so the cache is all the page has until the end.
  let release: () => void = () => undefined;
  const gate = new Promise<void>((resolve) => (release = resolve));
  await page.route('**/api/projects/p1/backlog?*', async (r) => {
    await gate;
    await r.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(view),
    });
  });
  const toast = (text: string) =>
    page.getByTestId(componentTestIds.toast).filter({ hasText: text });

  await dragOntoGroup(page, 'SHIP-141', 'Mobile sign-in');
  await expect(toast('SHIP-141 moved under Mobile sign-in')).toBeVisible();
  await dragOntoGroup(page, 'SHIP-141', 'No parent');
  await expect(toast('SHIP-141 no longer has a parent')).toBeVisible();
  // Back on the Feature it started under: the row has no parent now, so this is a change and is sent.
  await dragOntoGroup(page, 'SHIP-141', 'Checkout v2');
  await expect(toast('SHIP-141 moved under Checkout v2')).toBeVisible();

  expect(
    server.patches.map((p) => (p.body as { expectedRevision: number }).expectedRevision),
  ).toEqual([4, 5, 6]);
  await expect(page.getByTestId(componentTestIds.conflictBanner)).toHaveCount(0);
  release();
});

test('Tree_WhenDroppedOnAStoryUnderARemovedFeature_ShouldTakeTheGroupItSitsUnder', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, view);
  await mount(
    <>
      <TreeMode project={PROJECT} />
      <ToastHost />
    </>,
    route,
  );
  await expect(page.getByTestId(ids.row)).toHaveCount(6);

  await dragOnto(page, 'SHIP-141', 'SHIP-160');

  await expect.poll(() => server.reorders.length).toBe(1);
  const patch = server.patches[0].body as { changes: { parent: unknown } };
  expect(patch.changes.parent).toEqual({ value: idOf('SHIP-90') });
  expect(server.reorders[0].body).toEqual({ expectedRevision: 5, beforeItemId: idOf('SHIP-160') });
  await expect(page.getByTestId(componentTestIds.toast)).toContainText(
    'SHIP-141 moved under Storefront',
  );
});

test('Tree_WhenDroppedOnAGroupWithNoStories_ShouldShowTheRowRightUnderItAtOnce', async ({
  mount,
  page,
}) => {
  const bare = treeView(rows.filter((r) => r.key !== 'SHIP-150'));
  const server = await stubBacklog(page, bare);
  await mount(<TreeMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(5);
  // The PATCH is held, so what is on screen is the optimistic place.
  let release: () => void = () => undefined;
  const gate = new Promise<void>((resolve) => (release = resolve));
  await page.route('**/api/work-items/*', async (r) => {
    server.patches.push({ url: r.request().url(), body: r.request().postDataJSON() });
    await gate;
    await r.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ id: 'x', key: 'x', revision: 5, changes: [], notices: [] }),
    });
  });

  await dragOntoGroup(page, 'SHIP-144', 'Receipts');

  await expect.poll(() => server.patches.length).toBe(1);
  // The request is recorded in the drop turn. The optimistic row paints on the next commit, so wait for it.
  await expect
    .poll(async () => {
      const seen = await sequence(page);
      const at = seen.indexOf('SHIP-110');
      return seen.slice(at, at + 3);
    })
    .toEqual(['SHIP-110', 'SHIP-144', 'SHIP-102']);
  release();
});

test('Tree_WhenDraggedWithTheKeyboard_ShouldDropOnTheNearestRow', async ({ mount, page }) => {
  const server = await stubBacklog(page, view);
  await mount(<TreeMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(6);

  // SHIP-141 is picked up and moved down four steps of the keyboard sensor, which puts it over the Receipts group row.
  const dragged = page.getByTestId(ids.row).filter({ hasText: 'SHIP-141' });
  await dragged.getByTestId(ids.grip).focus();
  await page.keyboard.press('Space');
  // The first key is lost if it comes before the sensor listens: press it until the dragged row has moved.
  await pressUntil(page, 'ArrowDown', () =>
    expect(dragged).toHaveAttribute('style', /translate3d\(0px, [1-9]/, { timeout: 500 }),
  );
  for (let i = 1; i < KEY_STEPS; i++) await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Space');

  await expect.poll(() => server.patches.length + server.reorders.length).toBeGreaterThan(0);
  const body = server.patches[0].body as { changes: { parent: unknown } };
  expect(body.changes.parent).toEqual({ value: idOf('SHIP-110') });
});

const KEY_STEPS = 4;
