import { expect, test } from '@playwright/experimental-ct-react';
import { RETRO_CATEGORIES } from '../../../api/retros';
import { sprintRetro, stubRetros } from '../../retros/ct/retroFixtures';
import { retroSharedTestIds as ids } from '../testIds';
import { RetroBoardHarness } from './ct/RetroBoardHarness';

const points = sprintRetro.categories.flatMap((c) => c.groups.flatMap((g) => g.points));

test('RetroBoard_WhenMounted_ShouldRenderColumnsInTheFixedOrder', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetroBoardHarness />);
  const columns = page.getByTestId(ids.retroBoardColumn);
  await expect(columns).toHaveCount(4);
  const order = await columns.evaluateAll((els) => els.map((e) => e.getAttribute('data-category')));
  expect(order).toEqual([...RETRO_CATEGORIES]);
});

test('RetroBoard_WhenAFilterHidesEveryNote_ShouldStillShowToDecide', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetroBoardHarness hideAll />);
  await expect(page.getByTestId(ids.retroNote)).toHaveCount(0);
  const withOpen = sprintRetro.categories.filter((c) => c.toDecide > 0);
  expect(withOpen.length).toBeGreaterThan(0);
  for (const column of withOpen) {
    await expect(
      page
        .getByTestId(ids.retroBoardColumn)
        .and(page.locator(`[data-category="${column.category}"]`)),
    ).toContainText(`${column.toDecide} to decide`);
  }
});

test('RetroBoard_WhenAPointIsNamed_ShouldHighlightOnlyThatNote', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetroBoardHarness highlightedPointId={points[1].id} />);
  const marked = page.locator(`[data-testid="${ids.retroNote}"][data-highlighted="true"]`);
  await expect(marked).toHaveCount(1);
  await expect(marked).toHaveAttribute('data-point-id', points[1].id);
});

test('RetroBoard_WhenRendered_ShouldPutAddAfterTheLastCardOfEveryColumn', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<RetroBoardHarness />);
  const columns = page.getByTestId(ids.retroBoardColumn);
  await expect(columns).toHaveCount(4);
  for (let i = 0; i < 4; i++) {
    const column = columns.nth(i);
    await expect(column.getByTestId(ids.retroBoardAdd)).toHaveCount(1);
    const lastIsAdd = await column.evaluate((el) => {
      const last = el.lastElementChild as HTMLElement | null;
      return last?.getAttribute('data-testid') === 'retro-board-add';
    });
    expect(lastIsAdd).toBe(true);
    const notes = column.getByTestId(ids.retroNote);
    const noteBox = await notes.last().boundingBox();
    const addBox = await column.getByTestId(ids.retroBoardAdd).boundingBox();
    expect(addBox!.y).toBeGreaterThan(noteBox!.y + noteBox!.height - 1);
  }
});

test('RetroBoard_WhenGroupedByTheme_ShouldPutEachNoteUnderItsOwnThemeWithUnthemedLast', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<RetroBoardHarness />);
  for (const column of sprintRetro.categories) {
    const expected = column.groups.map((g) => ({
      heading: g.theme?.name ?? 'Unthemed',
      ids: g.points.map((p) => p.id),
    }));
    const actual = await page
      .locator(`[data-testid="${ids.retroBoardColumn}"][data-category="${column.category}"]`)
      .evaluate(
        (el, [chip, noteId]) => {
          const groups: { heading: string; ids: string[] }[] = [];
          for (const node of el.querySelectorAll(
            `[data-testid="${chip}"], [data-testid="${noteId}"]`,
          )) {
            if (node.getAttribute('data-testid') === chip) {
              groups.push({ heading: node.textContent?.trim() ?? '', ids: [] });
            } else {
              groups[groups.length - 1].ids.push(node.getAttribute('data-point-id') ?? '');
            }
          }
          return groups;
        },
        [ids.retroThemeChip, ids.retroNote],
      );
    expect(actual.map((g) => g.ids)).toEqual(expected.map((g) => g.ids));
    for (const [i, group] of expected.entries()) {
      expect(actual[i].heading).toContain(group.heading);
    }
  }
  expect(sprintRetro.categories.find((c) => c.category === 'improve')!.groups.at(-1)!.theme).toBe(
    null,
  );
});

test('RetroBoard_WhenFourColumnsAreNarrow_ShouldKeepEveryThemeHeadingAndNoteInsideItsColumn', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await stubRetros(page);
  await page.addStyleTag({ content: '#root { width: 860px; }' });
  await mount(<RetroBoardHarness />);
  const columns = page.getByTestId(ids.retroBoardColumn);
  await expect(columns).toHaveCount(4);
  const escaped = await columns.evaluateAll((els) =>
    els.flatMap((column) => {
      const edge = column.getBoundingClientRect().right;
      return [...column.querySelectorAll('*')]
        .filter((el) => el.getBoundingClientRect().right > edge + 0.5)
        .map((el) => el.getAttribute('data-testid') ?? el.tagName);
    }),
  );
  expect(escaped).toEqual([]);
});
