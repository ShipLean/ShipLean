import { expect, test } from '@playwright/experimental-ct-react';
import type { BoardViewDto } from '@shiplean/core/api/views';
import { BoardHarness } from '../../features/board/ct/BoardHarness';
import { COLUMNS, TOM, column, storiesBoard } from '../../features/board/ct/fixtures';
import { stubBoard } from '../../features/board/ct/stubApi';
import { boardTestIds as ids } from '../../features/board/testIds';
import { touchTestIds } from '../../touch/testIds';
import type { Page } from './shoot';

// The board toolbar, Filter, More and ⓘ sheets on a phone (SHIP-719, L719b), as element shots of this task's regions only,
// so the other board tasks' merges don't move them. Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/boardToolbar.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const route = { hooksConfig: { route: '/p/SHIP/boards/stories' } };
const MEI = { ...TOM, id: 'u-mei', name: 'Mei Tanaka', initials: 'MT' };

const phoneOnly = (page: Page & { viewportSize(): { width: number } | null }) =>
  expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);

// While any filter is set the server hides three cards, and says so on the first column.
function filtered(base: BoardViewDto, search: URLSearchParams): BoardViewDto {
  const on = [...search.keys()].some((name) => name !== 'older');
  return on
    ? {
        ...base,
        columns: base.columns.map((candidate, index) =>
          index === 0 ? column({ ...candidate, hiddenCount: 3 }) : candidate,
        ),
      }
    : base;
}

const withDefinition = (): BoardViewDto =>
  storiesBoard({
    columns: COLUMNS.map((candidate) =>
      candidate.id === 'col-ready'
        ? {
            ...candidate,
            definitionOfDoneHtml:
              '<ul><li>Acceptance criteria written</li><li>Sized by the team</li><li>No open questions</li></ul>',
          }
        : candidate,
    ),
  });

for (const theme of THEMES) {
  test(`BoardToolbarPhone_WhenFiltered_ShouldMatchItsScreenshot_board-toolbar_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubBoard(page, (search) => filtered(storiesBoard(), search), { members: [TOM, MEI] });
    await mount(<BoardHarness />, route);
    await page.getByTestId(ids.filterToggle).tap();
    await page.getByTestId(ids.filterPerson).filter({ hasText: 'Tom Okafor' }).tap();
    await page.getByTestId(touchTestIds.sheetClose).tap();
    await expect(page.getByTestId(ids.meta)).toContainText('Filtered: 11 of 14 cards');

    await expect(page.getByTestId(ids.toolbar)).toHaveScreenshot(`board-toolbar.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`BoardToolbarPhone_WhenFilterOpened_ShouldMatchItsScreenshot_board-filter_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubBoard(page, (search) => filtered(storiesBoard(), search), {
      members: [TOM, MEI],
      sprints: [
        { id: 's24', name: 'Sprint 24' },
        { id: 's25', name: 'Sprint 25' },
      ],
    });
    await mount(<BoardHarness />, route);
    await page.getByTestId(ids.filterToggle).tap();
    await page.getByTestId(ids.filterPerson).filter({ hasText: 'Mei Tanaka' }).tap();
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(
      sheet.getByTestId(ids.filterPerson).filter({ hasText: 'Mei Tanaka' }),
    ).toHaveAttribute('aria-pressed', 'true');
    await expect(sheet.getByTestId(ids.filterSprint).locator('option')).toHaveCount(4);

    await expect(sheet).toHaveScreenshot(`board-filter.${theme}.png`, { animations: 'disabled' });
  });

  test(`BoardToolbarPhone_WhenMoreOpened_ShouldMatchItsScreenshot_board-more_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubBoard(page, storiesBoard());
    await mount(<BoardHarness />, route);
    await page.getByTestId(ids.toolbar).getByTestId(touchTestIds.moreButton).tap();
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet.getByTestId(ids.moreSheet)).toBeVisible();

    await expect(sheet).toHaveScreenshot(`board-more.${theme}.png`, { animations: 'disabled' });
  });

  test(`BoardToolbarPhone_WhenInfoTapped_ShouldMatchItsScreenshot_board-dod_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubBoard(page, withDefinition());
    await mount(<BoardHarness />, route);
    await page
      .getByTestId(ids.columnHeader)
      .filter({ hasText: 'Ready' })
      .getByTestId(ids.dodOpen)
      .tap();
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet.getByTestId(ids.dodBody)).toContainText('Sized by the team');

    await expect(sheet).toHaveScreenshot(`board-dod.${theme}.png`, { animations: 'disabled' });
  });
}
