import { expect, test } from '@playwright/experimental-ct-react';
import type { BoardSettingsDto, BoardSettingsPut } from '../../../../api/boards';
import type { BoardColumnJson } from '../../../../api/persisted';
import { settingsDialogTestIds as dialogIds } from '../../../settingsDialog/testIds';
import { BoardSettingsDialog } from '../../BoardSettingsDialog';
import {
  BOARD_ID,
  boardSettingsDto,
  emptySettings,
  PROJECT_ID,
} from '../../ct/boardSettingsFixtures';
import { boardSettingsTestIds as ids } from '../../testIds';

const column = (id: string, name: string): BoardColumnJson => ({
  id,
  name,
  wipLimit: null,
  split: false,
  definitionOfDone: '',
  stateMappings: [{ type: 'story', stateId: 's-new' }],
});

const NAMES: Record<string, string> = {
  c1: 'New',
  c2: 'Ready',
  c3: 'Done',
  c4: 'Closed',
};

interface StubbedRoute {
  request(): { method(): string; postDataJSON(): unknown };
  fulfill(response: { json: unknown; status?: number }): Promise<void>;
}
interface Routable {
  route(url: string, handler: (route: StubbedRoute) => Promise<void>): Promise<unknown>;
}

async function stubBoard(
  page: Routable,
  counts: Record<string, number>,
  boardDelayMs = 0,
  columnIds = ['c1', 'c2', 'c3'],
): Promise<BoardSettingsPut[]> {
  const dto: BoardSettingsDto = boardSettingsDto({
    states: [{ type: 'story', states: [{ id: 's-new', name: 'New', category: 'proposed' }] }],
    settings: {
      ...emptySettings(),
      columns: columnIds.map((id) => column(id, NAMES[id])),
    },
  });
  const puts: BoardSettingsPut[] = [];
  await page.route(`**/api/projects/${PROJECT_ID}/boards`, (route) =>
    route.fulfill({ json: [{ id: BOARD_ID, level: 'stories', name: 'stories' }] }),
  );
  await page.route(`**/api/boards/${BOARD_ID}`, async (route) => {
    if (boardDelayMs > 0) await new Promise((resolve) => setTimeout(resolve, boardDelayMs));
    return route.fulfill({
      json: {
        boardId: BOARD_ID,
        columns: Object.entries(counts).map(([id, count]) => ({ id, count })),
      },
    });
  });
  await page.route(`**/api/boards/${BOARD_ID}/settings`, async (route) => {
    if (route.request().method() === 'GET') return route.fulfill({ json: dto });
    const body = route.request().postDataJSON() as BoardSettingsPut;
    puts.push(body);
    return route.fulfill({
      json: { board: { ...dto, settings: body.settings }, mergedSections: [] },
    });
  });
  return puts;
}

const mountColumns = (mount: (component: React.JSX.Element) => Promise<unknown>) =>
  mount(
    <BoardSettingsDialog
      projectId={PROJECT_ID}
      level="stories"
      section="columns"
      onSectionChange={() => undefined}
      onClose={() => undefined}
    />,
  );

test('ColumnsPane_WhenColumnHoldsCards_ShouldSayHowManyAndKeepIt', async ({ mount, page }) => {
  await stubBoard(page, { c1: 0, c2: 3, c3: 1 });
  await mountColumns(mount);
  await expect(page.getByTestId(ids.columnRow)).toHaveCount(3);

  await page.getByTestId(ids.columnDelete).nth(1).click();

  await expect(page.getByTestId(ids.columnBlocked)).toContainText('Ready has 3 cards');
  await expect(page.getByTestId(ids.columnRow)).toHaveCount(3);
  await expect(page.getByTestId(ids.columnDeleted)).toHaveCount(0);

  await page.getByTestId(ids.columnBlockedDismiss).click();
  await expect(page.getByTestId(ids.columnBlocked)).toHaveCount(0);
});

test('ColumnsPane_WhenColumnHoldsOneCard_ShouldSaySingular', async ({ mount, page }) => {
  await stubBoard(page, { c2: 1 });
  await mountColumns(mount);
  await expect(page.getByTestId(ids.columnRow)).toHaveCount(3);

  await page.getByTestId(ids.columnDelete).nth(1).click();

  await expect(page.getByTestId(ids.columnBlocked)).toContainText('Ready has 1 card,');
});

test('ColumnsPane_WhenColumnHoldsNoCards_ShouldDeleteIt', async ({ mount, page }) => {
  const puts = await stubBoard(page, { c1: 2, c2: 0, c3: 4 });
  await mountColumns(mount);
  await expect(page.getByTestId(ids.columnRow)).toHaveCount(3);

  await page.getByTestId(ids.columnDelete).nth(1).click();
  await expect(page.getByTestId(ids.columnRow)).toHaveCount(2);
  await expect(page.getByTestId(ids.columnBlocked)).toHaveCount(0);
  await page.getByTestId(dialogIds.save).click();

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.columns.map((c) => c.id)).toEqual(['c1', 'c3']);
});

test('ColumnsPane_WhenTwoDeletesClickedWhileTheBoardLoads_ShouldDropBoth', async ({
  mount,
  page,
}) => {
  const puts = await stubBoard(page, { c1: 0, c2: 0, c3: 0, c4: 0 }, 300, ['c1', 'c2', 'c3', 'c4']);
  await mountColumns(mount);
  await expect(page.getByTestId(ids.columnRow)).toHaveCount(4);

  await page.getByTestId(ids.columnDelete).nth(1).click();
  await page.getByTestId(ids.columnDelete).nth(2).click();

  await expect(page.getByTestId(ids.columnRow)).toHaveCount(2);
  await expect(page.getByTestId(ids.columnDeleted)).toHaveCount(2);
  await page.getByTestId(dialogIds.save).click();
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.columns.map((c) => c.id)).toEqual(['c1', 'c4']);
});

test('ColumnsPane_WhenColumnRenamedWhileTheBoardLoads_ShouldKeepTheRenameAfterTheDelete', async ({
  mount,
  page,
}) => {
  const puts = await stubBoard(page, { c1: 0, c2: 0, c3: 0, c4: 0 }, 300, ['c1', 'c2', 'c3', 'c4']);
  await mountColumns(mount);
  await expect(page.getByTestId(ids.columnRow)).toHaveCount(4);

  await page.getByTestId(ids.columnDelete).nth(1).click();
  await page.getByTestId(ids.columnName).nth(3).fill('Finished');

  await expect(page.getByTestId(ids.columnRow)).toHaveCount(3);
  await page.getByTestId(dialogIds.save).click();
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.columns.map((c) => c.name)).toEqual(['New', 'Done', 'Finished']);
});
