import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '../../../api/projects';
import type { RetroItemDto, RetroPointDto } from '../../../api/retros';
import { retroSharedTestIds as shared } from '../../retroShared/testIds';
import { retroItem, stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { RetroItemPanel } from './RetroItemPanel';

const project = { id: 'p1', key: 'SHIP' } as ProjectDto;
const route = (query = '') => ({ hooksConfig: { route: `/p/SHIP/retros/items/SHIP-101${query}` } });

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
  await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route());
  await expect(page.getByTestId(shared.retroNote)).toHaveCount(allPoints.length);
  await page
    .getByTestId(ids.retroItemFilter)
    .getByRole('button', { name: /Second agent/ })
    .click();
  await expect(page.getByTestId(shared.retroNote)).toHaveCount(inOther.length);
});

test('RetroItemPanel_WhenOpenedWithRetro_ShouldShowThatRetro', async ({ mount, page }) => {
  await serve(page, twoRetros);
  await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route(`?retro=${OTHER}`));
  await expect(page.getByTestId(shared.retroNote)).toHaveCount(inOther.length);
  await expect(
    page.getByTestId(ids.retroItemFilter).getByRole('button', { name: /Second agent/ }),
  ).toHaveAttribute('aria-pressed', 'true');
});

test('RetroItemPanel_WhenToDecideChosen_ShouldShowOnlyUndecidedPoints', async ({ mount, page }) => {
  await serve(page, twoRetros);
  await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route('?decide=open'));
  const undecided = allPoints.filter((p) => p.decision === 'undecided').length;
  await expect(page.getByTestId(shared.retroNote)).toHaveCount(undecided);
});

test('RetroItemPanel_WhenOpenedWithPoint_ShouldHighlightTheNote', async ({ mount, page }) => {
  await serve(page, twoRetros);
  const id = allPoints[allPoints.length - 1].id;
  await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route(`?point=${id}`));
  const marked = page.locator(`[data-testid="${shared.retroNote}"][data-highlighted="true"]`);
  await expect(marked).toHaveCount(1);
  await expect(marked).toHaveAttribute('data-point-id', id);
});

test('RetroItemPanel_WhenActionItemClicked_ShouldOpenTheDialog', async ({ mount, page }) => {
  await serve(page, twoRetros);
  await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route());
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
  await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route());
  await expect(page.getByTestId(ids.retroItemPanel)).toBeVisible();
  await expect(page.getByRole('heading', { name: twoRetros.item.title })).toBeVisible();
  await expect(page.getByTestId(shared.retroNote)).toHaveCount(allPoints.length);
  await expect(page.getByText('Part of')).toHaveCount(0);
});

test('RetroItemPanel_WhenBugIsInAFeature_ShouldLinkTheFeature', async ({ mount, page }) => {
  await serve(page, {
    ...twoRetros,
    item: { ...twoRetros.item, type: { id: 'bug', name: 'Bug' } },
    parent: { ...twoRetros.item, key: 'SHIP-180', title: 'Saved cards' },
    children: [],
  });
  await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route());
  await expect(page.getByRole('link', { name: 'SHIP-180 Saved cards' })).toHaveAttribute(
    'href',
    '/p/SHIP/retros/items/SHIP-180',
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
  await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route());
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
  await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route());
  await expect(page.getByTestId(ids.retroItemChildRows)).toBeVisible();
  const board = await page.getByTestId(shared.retroBoard).boundingBox();
  const logs = await page.getByTestId(ids.retroItemLogs).boundingBox();
  const rows = await page.getByTestId(ids.retroItemChildRows).boundingBox();
  expect(board!.y + board!.height).toBeLessThanOrEqual(logs!.y);
  expect(logs!.y + logs!.height).toBeLessThanOrEqual(rows!.y);
});

/** Answers the board's POST of a new point with `point`, over the stub's own answer. */
async function answerAddWith(page: Parameters<typeof stubRetros>[0], point: RetroPointDto) {
  await page.route(/\/api\/projects\/[^/]+\/retros\/points$/, (r) =>
    r.request().method() === 'POST'
      ? r.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify(point) })
      : r.fallback(),
  );
}

async function addUnder(page: Parameters<typeof stubRetros>[0], category: string, text: string) {
  const column = page.locator(
    `[data-testid="${shared.retroBoardColumn}"][data-category="${category}"]`,
  );
  await column.getByTestId(shared.retroBoardAdd).click();
  await page.getByTestId(shared.retroBoardAddText).fill(text);
  await page.getByTestId(shared.retroBoardAddSave).click();
}

test('RetroItemPanel_WhenAddedUnderAColumnToAnotherRetro_ShouldClearTheRetroFilter', async ({
  mount,
  page,
}) => {
  await serve(page, twoRetros);
  await answerAddWith(page, {
    ...allPoints.find((p) => p.category === 'improve')!,
    retroId: firstCard.id,
  });
  await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route(`?retro=${OTHER}`));
  const retroFilter = page.getByTestId(ids.retroItemFilter);
  await expect(retroFilter.getByRole('button', { name: /Second agent/ })).toHaveAttribute(
    'aria-pressed',
    'true',
  );

  await addUnder(page, 'improve', 'A point written under the filter');

  await expect(retroFilter.getByRole('button', { name: 'All retros' })).toHaveAttribute(
    'aria-pressed',
    'true',
  );
});

test('RetroItemPanel_WhenWentWellAddedUnderToDecide_ShouldClearToDecide', async ({
  mount,
  page,
}) => {
  await serve(page, twoRetros);
  await answerAddWith(page, {
    ...allPoints.find((p) => p.category === 'wentWell')!,
    retroId: firstCard.id,
  });
  await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route('?decide=open'));
  const pointsFilter = page.getByTestId(ids.retroToDecideFilter);
  await expect(pointsFilter.getByRole('button', { name: 'To decide' })).toHaveAttribute(
    'aria-pressed',
    'true',
  );

  await addUnder(page, 'wentWell', 'Something went well');

  await expect(pointsFilter.getByRole('button', { name: 'All points' })).toHaveAttribute(
    'aria-pressed',
    'true',
  );
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
    await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route());
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

test('RetroItemPanel_WhenRendered_ShouldDrawActionItemAsAFilledPrimaryButton', async ({
  mount,
  page,
}) => {
  await serve(page, twoRetros);
  await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route());
  await expect(page.getByTestId(ids.retroItemAddAction)).not.toHaveCSS(
    'background-color',
    'rgba(0, 0, 0, 0)',
  );
});
