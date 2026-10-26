import { expect, test } from '@playwright/experimental-ct-react';
import { ToastHost } from '../../../components/ToastHost';
import { componentTestIds } from '../../../components/testIds';
import { PROJECT, treeRows, treeView } from '../ct/fixtures';
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
interface TreePage {
  mouse: {
    move(x: number, y: number, options?: { steps: number }): Promise<void>;
    down(): Promise<void>;
    up(): Promise<void>;
  };
  getByTestId(id: string): {
    filter(options: { hasText: string }): { first(): { boundingBox(): Promise<Box | null> } };
  };
}

// The sequence of keys on screen: groups and stories alike, in order ('' is the No parent group).
const sequence = (page: {
  getByTestId(id: string): { evaluate<T>(f: (e: Element) => T): Promise<T> };
}) =>
  page
    .getByTestId(ids.list)
    .evaluate((el) =>
      [...el.querySelectorAll('tbody tr')].map((tr) => tr.getAttribute('data-key') ?? ''),
    );

const gripOf = async (page: TreePage, key: string) =>
  (await page.getByTestId(ids.row).filter({ hasText: key }).first().boundingBox())!;

async function dragTo(page: TreePage, key: string, to: Box) {
  const grip = await gripOf(page, key);
  const startX = grip.x + 12;
  const startY = grip.y + grip.height / 2;
  await page.mouse.move(startX, startY);
  await page.mouse.down();
  await page.mouse.move(startX + 6, startY + 10, { steps: 4 });
  await page.mouse.move(to.x + to.width / 2, to.y + to.height / 2, { steps: 12 });
  await page.mouse.up();
}

// Picks a story up by its grip and puts it down on another story's row.
async function dragOnto(page: TreePage, key: string, target: string) {
  await dragTo(page, key, await gripOf(page, target));
}

// Puts a story down on a group row, found by its text.
async function dragOntoGroup(page: TreePage, key: string, groupText: string) {
  const to = (await page
    .getByTestId(ids.treeGroup)
    .filter({ hasText: groupText })
    .first()
    .boundingBox())!;
  await dragTo(page, key, to);
}

test('Tree_WhenLoaded_ShouldShowGroupsStoriesAndNoParentLast', async ({ mount, page }) => {
  const server = await stubBacklog(page, view);
  await mount(<TreeMode project={PROJECT} />, route);

  await expect(page.getByTestId(ids.row)).toHaveCount(6);
  expect(await sequence(page)).toEqual(
    [
      'SHIP-90',
      'SHIP-160',
      'SHIP-101',
      'SHIP-141',
      'SHIP-144',
      'SHIP-110',
      'SHIP-150',
      'SHIP-102',
      'SHIP-129',
      'SHIP-170',
    ].flatMap((key) => (key === 'SHIP-170' ? ['', key] : [key])),
  );
  expect(server.reads[0]).toContain('mode=tree');
  const groups = page.getByTestId(ids.treeGroup);
  await expect(groups).toHaveCount(5);
  await expect(groups.last()).toHaveAttribute('data-kind', 'noParentGroup');
  await expect(groups.nth(1).getByTestId(ids.treeRollup)).toHaveText('3 items · Σ 11 pts');
  await expect(groups.nth(3).getByTestId(ids.treeRollup)).toHaveText('1 item');
  await expect(groups.nth(3).getByTestId(ids.treeRollup)).toHaveAttribute('data-warn', 'true');
  await expect(groups.last().getByTestId(ids.treeRollup)).toHaveText('1 item · Σ 3 pts');
});

test('Tree_WhenShown_ShouldHideRankAndParentColumns', async ({ mount, page }) => {
  await stubBacklog(page, view);
  await mount(<TreeMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row).first()).toBeVisible();
  const columns = await page
    .getByTestId(ids.columnHeader)
    .evaluateAll((els) => els.map((e) => e.getAttribute('data-column')));
  expect(columns).not.toContain('rank');
  expect(columns).not.toContain('parent');
  expect(columns).toContain('title');
  await expect(page.getByTestId(ids.rank)).toHaveCount(0);
});

test('Tree_WhenGroupCollapsed_ShouldHideItsRowsAndShowThemAgain', async ({ mount, page }) => {
  await stubBacklog(page, view);
  await mount(<TreeMode project={PROJECT} />, route);
  const checkout = page.getByTestId(ids.treeGroup).nth(1);
  await expect(checkout.getByTestId(ids.treeCaret)).toHaveAttribute('aria-expanded', 'true');

  await checkout.getByTestId(ids.treeCaret).click();

  await expect(checkout.getByTestId(ids.treeCaret)).toHaveAttribute('aria-expanded', 'false');
  expect(await sequence(page)).not.toContain('SHIP-141');
  expect(await sequence(page)).not.toContain('SHIP-150');
  expect(await sequence(page)).toContain('SHIP-129');

  await checkout.getByTestId(ids.treeCaret).click();
  expect(await sequence(page)).toContain('SHIP-150');
});

test('Tree_WhenDroppedOnFeature_ShouldPatchTheParentOnly', async ({ mount, page }) => {
  const server = await stubBacklog(page, view);
  await mount(<TreeMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(6);

  await dragOntoGroup(page, 'SHIP-141', 'Mobile sign-in');

  await expect.poll(() => server.patches.length).toBe(1);
  expect(server.patches[0].url).toContain(`/api/work-items/${idOf('SHIP-141')}`);
  const body = server.patches[0].body as { expectedRevision: number; changes: { parent: unknown } };
  expect(body.expectedRevision).toBe(4);
  expect(body.changes.parent).toEqual({ value: idOf('SHIP-102') });
  expect(server.reorders).toHaveLength(0);
});

test('Tree_WhenDroppedOnStoryInOtherFeature_ShouldPatchThenReorderAgainstTheNewRevision', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, view);
  await mount(<TreeMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(6);

  await dragOnto(page, 'SHIP-141', 'SHIP-129');

  await expect.poll(() => server.reorders.length).toBe(1);
  expect(server.patches).toHaveLength(1);
  const patch = server.patches[0].body as { changes: { parent: unknown } };
  expect(patch.changes.parent).toEqual({ value: idOf('SHIP-102') });
  expect(server.reorders[0].url).toContain(`/api/work-items/${idOf('SHIP-141')}/reorder`);
  expect(server.reorders[0].body).toEqual({
    expectedRevision: 5,
    beforeItemId: idOf('SHIP-129'),
  });
});

test('Tree_WhenDroppedOnStoryOfTheSameParent_ShouldOnlyReorder', async ({ mount, page }) => {
  const server = await stubBacklog(page, view);
  await mount(<TreeMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(6);

  await dragOnto(page, 'SHIP-144', 'SHIP-141');

  await expect.poll(() => server.reorders.length).toBe(1);
  expect(server.patches).toHaveLength(0);
  expect(server.reorders[0].body).toEqual({
    expectedRevision: 1,
    beforeItemId: idOf('SHIP-141'),
  });
});

test('Tree_WhenDroppedOnNoParent_ShouldClearTheParentEvenUnderARemovedFeature', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, view);
  await mount(<TreeMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(6);

  await dragOntoGroup(page, 'SHIP-160', 'No parent');

  await expect.poll(() => server.patches.length).toBe(1);
  const body = server.patches[0].body as { expectedRevision: number; changes: { parent: unknown } };
  expect(body.expectedRevision).toBe(7);
  expect(body.changes.parent).toEqual({ value: null });
  expect(server.reorders).toHaveLength(0);
});

test('Tree_WhenParentTypeRefused_ShouldToastTheMessageAndPutTheRowBack', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, view, {
    patchAnswers: [
      {
        status: 400,
        body: { error: 'A User Story can only sit under a Feature.', code: 'validation' },
      },
    ],
  });
  await mount(
    <>
      <TreeMode project={PROJECT} />
      <ToastHost />
    </>,
    route,
  );
  await expect(page.getByTestId(ids.row)).toHaveCount(6);
  const before = await sequence(page);
  const reads = server.reads.length;

  await dragOntoGroup(page, 'SHIP-141', 'Storefront');

  await expect(page.getByTestId(componentTestIds.toast)).toContainText(
    'A User Story can only sit under a Feature.',
  );
  await expect.poll(() => server.reads.length).toBeGreaterThan(reads);
  expect(await sequence(page)).toEqual(before);
  expect(server.reorders).toHaveLength(0);
});

test('Tree_WhenMoved_ShouldToastWhereItWent', async ({ mount, page }) => {
  await stubBacklog(page, view);
  await mount(
    <>
      <TreeMode project={PROJECT} />
      <ToastHost />
    </>,
    route,
  );
  await expect(page.getByTestId(ids.row)).toHaveCount(6);
  await dragOntoGroup(page, 'SHIP-141', 'Mobile sign-in');
  await expect(page.getByTestId(componentTestIds.toast)).toContainText(
    'SHIP-141 moved under Mobile sign-in',
  );
});

test('Tree_WhenFilterOn_ShouldSendTheFilterAndTurnMovingOff', async ({ mount, page }) => {
  const server = await stubBacklog(page, view);
  await mount(<TreeMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(6);

  await page.getByTestId(ids.filterButton).click();

  await expect(page.getByTestId(ids.dragOffBanner)).toBeVisible();
  await expect.poll(() => server.reads.some((read) => read.includes('filter=%7B'))).toBe(true);
  await expect(page.getByTestId(ids.grip).first()).toBeDisabled();
});

test('Tree_WhenCompletedTicked_ShouldAskForCompletedAndCountThemInGroups', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, view);
  await mount(<TreeMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(6);

  await page.getByTestId(ids.completedToggle).check();

  await expect
    .poll(() => server.reads.some((read) => read.includes('includeCompleted=true')))
    .toBe(true);
  await expect(page.getByTestId(ids.treeGroup).nth(1).getByTestId(ids.treeRollup)).toHaveText(
    '4 items · Σ 11 pts',
  );
});
