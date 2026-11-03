import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '../../../api/projects';
import type { RetroItemDto } from '../../../api/retros';
import { retroSharedTestIds as shared } from '../../retroShared/testIds';
import { retroItem, stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { ItemAddedHarness } from './ct/ItemAddedHarness';
import { RetroItemPanel } from './RetroItemPanel';

const project = { id: 'p1', key: 'SHIP' } as ProjectDto;
const route = (query = '') => ({ hooksConfig: { route: `/p/SHIP/retros/items/SHIP-180${query}` } });

const OTHER = 'second-retro';
const [firstCard] = retroItem.cards;

/** The fixture with every other point moved into a second retro, so a retro filter has something to narrow. */
const twoRetros: RetroItemDto = {
  ...retroItem,
  cards: [
    firstCard,
    {
      ...firstCard,
      id: OTHER,
      author: { ...firstCard.author, agentName: 'Second agent', role: 'subAgent' },
    },
  ],
  categories: retroItem.categories.map((column) => ({
    ...column,
    groups: column.groups.map((group) => ({
      ...group,
      points: group.points.map((point, index) =>
        index % 2 === 1 ? { ...point, retroId: OTHER } : point,
      ),
    })),
  })),
};
const allPoints = twoRetros.categories.flatMap((c) => c.groups.flatMap((g) => g.points));
const inOther = allPoints.filter((p) => p.retroId === OTHER);

async function serve(page: Parameters<typeof stubRetros>[0], item: RetroItemDto) {
  await stubRetros(page);
  await page.route(/\/api\/projects\/[^/]+\/retros\/items\/[^/]+$/, (r) =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(item) }),
  );
}

test('RetroItemPanel_WhenRetroFilterClicked_ShouldNarrowTheBoard', async ({ mount, page }) => {
  await serve(page, twoRetros);
  await mount(<RetroItemPanel project={project} itemKey="SHIP-180" />, route());
  await expect(page.getByTestId(shared.retroNote)).toHaveCount(allPoints.length);
  await page
    .getByTestId(ids.retroItemFilter)
    .getByRole('button', { name: /Second agent/ })
    .click();
  await expect(page.getByTestId(shared.retroNote)).toHaveCount(inOther.length);
});

test('RetroItemPanel_WhenOpenedWithRetro_ShouldShowThatRetro', async ({ mount, page }) => {
  await serve(page, twoRetros);
  await mount(<RetroItemPanel project={project} itemKey="SHIP-180" />, route(`?retro=${OTHER}`));
  await expect(page.getByTestId(shared.retroNote)).toHaveCount(inOther.length);
  await expect(
    page.getByTestId(ids.retroItemFilter).getByRole('button', { name: /Second agent/ }),
  ).toHaveAttribute('aria-pressed', 'true');
});

test('RetroItemPanel_WhenToDecideChosen_ShouldShowOnlyUndecidedPoints', async ({ mount, page }) => {
  await serve(page, twoRetros);
  await mount(<RetroItemPanel project={project} itemKey="SHIP-180" />, route('?decide=open'));
  const undecided = allPoints.filter((p) => p.decision === 'undecided').length;
  await expect(page.getByTestId(shared.retroNote)).toHaveCount(undecided);
});

test('RetroItemPanel_WhenOpenedWithPoint_ShouldHighlightTheNote', async ({ mount, page }) => {
  await serve(page, twoRetros);
  const id = allPoints[4].id;
  await mount(<RetroItemPanel project={project} itemKey="SHIP-180" />, route(`?point=${id}`));
  const marked = page.locator(`[data-testid="${shared.retroNote}"][data-highlighted="true"]`);
  await expect(marked).toHaveCount(1);
  await expect(marked).toHaveAttribute('data-point-id', id);
});

test('RetroItemPanel_WhenActionItemClicked_ShouldOpenTheDialog', async ({ mount, page }) => {
  await serve(page, twoRetros);
  await mount(<RetroItemPanel project={project} itemKey="SHIP-180" />, route());
  await page.getByTestId(ids.retroItemAddAction).click();
  await expect(page.getByTestId(shared.retroActionDialog)).toBeVisible();
});

test('RetroItemPanel_WhenBugHasNoFeature_ShouldShowNoPartOfLine', async ({ mount, page }) => {
  await serve(page, {
    ...twoRetros,
    item: { ...twoRetros.item, type: { id: 'bug', name: 'Bug' } },
    parent: null,
    children: [],
  });
  await mount(<RetroItemPanel project={project} itemKey="SHIP-180" />, route());
  await expect(page.getByTestId(ids.retroItemPanel)).toBeVisible();
  await expect(page.getByRole('heading', { name: twoRetros.item.title })).toBeVisible();
  await expect(page.getByTestId(shared.retroNote)).toHaveCount(allPoints.length);
  await expect(page.getByText('Part of')).toHaveCount(0);
});

test('RetroItemPanel_WhenBugIsInAFeature_ShouldLinkTheFeature', async ({ mount, page }) => {
  await serve(page, {
    ...twoRetros,
    item: { ...twoRetros.item, type: { id: 'bug', name: 'Bug' } },
    parent: { ...twoRetros.item, key: 'SHIP-101', title: 'Checkout v2' },
    children: [],
  });
  await mount(<RetroItemPanel project={project} itemKey="SHIP-180" />, route());
  await expect(page.getByRole('link', { name: 'SHIP-101 Checkout v2' })).toHaveAttribute(
    'href',
    '/p/SHIP/retros/items/SHIP-101',
  );
});

test('RetroItemPanel_WhenItemHasNoRetros_ShouldStillRenderTheBoardToAdd', async ({
  mount,
  page,
}) => {
  await serve(page, {
    ...twoRetros,
    cards: [],
    categories: twoRetros.categories.map((c) => ({ ...c, groups: [], toDecide: 0 })),
    children: [],
    parent: null,
  });
  await mount(<RetroItemPanel project={project} itemKey="SHIP-180" />, route());
  await expect(page.getByTestId(shared.retroBoardColumn)).toHaveCount(4);
  await expect(page.getByTestId(shared.retroBoardAdd)).toHaveCount(4);
});

test('RetroItemPanel_WhenItemHasLogsAndStories_ShouldShowLogsBelowTheBoardBeforeTheStoryRows', async ({
  mount,
  page,
}) => {
  const [child] = retroItem.children;
  await serve(page, {
    ...twoRetros,
    cards: [
      {
        ...firstCard,
        failureLog: { blockers: ['Port 5432 was taken'], assumptionsDisproved: [], friction: [] },
      },
      twoRetros.cards[1],
    ],
    children: [child],
  });
  await mount(<RetroItemPanel project={project} itemKey="SHIP-180" />, route());
  await expect(page.getByTestId(ids.retroItemChildRows)).toBeVisible();
  const board = await page.getByTestId(shared.retroBoard).boundingBox();
  const logs = await page.getByTestId(ids.retroItemLogs).boundingBox();
  const rows = await page.getByTestId(ids.retroItemChildRows).boundingBox();
  expect(board!.y + board!.height).toBeLessThanOrEqual(logs!.y);
  expect(logs!.y + logs!.height).toBeLessThanOrEqual(rows!.y);
});

test('ItemAddedHarness_WhenAddedPointWouldBeHidden_ShouldClearTheFilter', async ({
  mount,
  page,
}) => {
  await mount(<ItemAddedHarness />, { hooksConfig: { route: '/p/SHIP/retros/items/X?retro=r1' } });
  await expect(page.getByTestId('address')).toHaveText('retro=r1 decide=false');
  await page.getByRole('button', { name: 'added to another retro' }).click();
  await expect(page.getByTestId('address')).toHaveText('retro= decide=false');
});

test('ItemAddedHarness_WhenWentWellAddedUnderToDecide_ShouldClearDecide', async ({
  mount,
  page,
}) => {
  await mount(<ItemAddedHarness />, {
    hooksConfig: { route: '/p/SHIP/retros/items/X?retro=r1&decide=open' },
  });
  await page.getByRole('button', { name: 'added a went well' }).click();
  await expect(page.getByTestId('address')).toHaveText('retro=r1 decide=false');
});

for (const width of [1280, 1440]) {
  test(`RetroItemPanel_When${width}Wide_ShouldKeepToolbarInsideTheWindowWithoutOverflow`, async ({
    mount,
    page,
  }) => {
    await page.setViewportSize({ width, height: 800 });
    const tall: RetroItemDto = {
      ...twoRetros,
      categories: twoRetros.categories.map((c) => ({
        ...c,
        groups: c.groups.map((g) => ({
          ...g,
          points: Array.from({ length: 6 }, (_, i) =>
            g.points.map((p) => ({ ...p, id: `${p.id}-${i}` })),
          ).flat(),
        })),
      })),
    };
    await serve(page, tall);
    await mount(<RetroItemPanel project={project} itemKey="SHIP-180" />, route());
    await expect(page.getByTestId(shared.retroBoard)).toBeVisible();
    const box = await page.getByTestId(ids.retroItemAddAction).boundingBox();
    expect(box).not.toBeNull();
    expect(box!.y + box!.height).toBeLessThanOrEqual(800);
    expect(box!.x + box!.width).toBeLessThanOrEqual(width);
    const board = await page.getByTestId(shared.retroBoard).boundingBox();
    expect(board!.height).toBeGreaterThan(800);
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
  });
}
