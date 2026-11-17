import { expect } from '@playwright/experimental-ct-react';
import type { BoardViewDto } from '@shiplean/core/api/views';
import { column, storiesBoard, TOM } from '../../features/board/ct/fixtures';
import { stubBoard } from '../../features/board/ct/stubApi';
import { featuresWithHealth, filteredByHealth } from '../../features/board/filter/ct/healthBoard';
import { boardTestIds as ids } from '../../features/board/testIds';
import { touchTestIds } from '../../touch/testIds';
import { stubShell } from './shellStub';
import { phonePageShots } from './shootPhone';

// The board's toolbar with a filter on, and the Features board's Health filter, as whole pages at the real 390px width
// (SHIP-1827): the right-aligned Filter and the bordered ⋯ only show against the page's own gutters. Regenerate with
// scripts/update-page-shots.sh src/pages/screenshots/boardFilter.phone.spec.tsx.

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

phonePageShots([
  {
    name: 'board-filter-active',
    route: '/p/SHIP/boards/stories',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubBoard(page, (search) => filtered(storiesBoard(), search), { members: [TOM] });
    },
    ready: (page) => expect(page.getByTestId(ids.card).first()).toBeVisible(),
    act: async (page) => {
      await page.getByTestId(ids.filterToggle).tap();
      await page.getByTestId(ids.filterPerson).filter({ hasText: 'Tom Okafor' }).tap();
      await page.getByTestId(touchTestIds.sheetClose).tap();
      await expect(page.getByTestId(ids.meta)).toContainText('Filtered: 11 of 14 cards');
    },
  },
  {
    name: 'board-filter-health',
    route: '/p/SHIP/boards/features',
    shell: true,
    arrange: async (page) => {
      await stubShell(page);
      await stubBoard(page, (search) => filteredByHealth(featuresWithHealth(), search), {
        level: 'features',
        members: [TOM],
        epics: [{ id: 'e-90', title: 'Seller payouts' }],
      });
    },
    ready: (page) => expect(page.getByTestId(ids.card).first()).toBeVisible(),
    act: async (page) => {
      await page.getByTestId(ids.filterToggle).tap();
      await page
        .getByTestId(touchTestIds.sheet)
        .getByTestId(ids.filterHealth)
        .selectOption('atRisk');
      await expect(page.getByTestId(ids.filterCount)).toContainText('Showing 12 of 14 cards');
    },
  },
]);
