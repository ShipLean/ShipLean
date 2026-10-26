import { expect, test } from '@playwright/experimental-ct-react';
import type { BoardSettingsDto, BoardSettingsPut } from '../../../api/boards';
import type { BoardColumnJson } from '../../../api/persisted';
import { settingsDialogTestIds as dialogIds } from '../../settingsDialog/testIds';
import { BoardSettingsDialog } from '../BoardSettingsDialog';
import { BOARD_ID, boardSettingsDto, emptySettings, PROJECT_ID } from '../ct/boardSettingsFixtures';
import { boardSettingsTestIds as ids } from '../testIds';

const story = {
  new: { id: 's-new', name: 'New', category: 'proposed' },
  active: { id: 's-active', name: 'Active', category: 'inProgress' },
  closed: { id: 's-closed', name: 'Closed', category: 'completed' },
  removed: { id: 's-removed', name: 'Removed', category: 'removed' },
} as const;
const bug = {
  new: { id: 'b-new', name: 'New', category: 'proposed' },
  triaged: { id: 'b-triaged', name: 'Triaged', category: 'proposed' },
  active: { id: 'b-active', name: 'Active', category: 'inProgress' },
  closed: { id: 'b-closed', name: 'Closed', category: 'completed' },
} as const;

const column = (id: string, name: string, storyId: string, bugId: string): BoardColumnJson => ({
  id,
  name,
  wipLimit: null,
  split: false,
  definitionOfDone: '',
  stateMappings: [
    { type: 'story', stateId: storyId },
    { type: 'bug', stateId: bugId },
  ],
});

function storiesBoard(): BoardSettingsDto {
  return boardSettingsDto({
    states: [
      { type: 'story', states: Object.values(story) },
      { type: 'bug', states: Object.values(bug) },
    ],
    settings: {
      ...emptySettings(),
      columns: [
        column('col-new', 'New', story.new.id, bug.new.id),
        { ...column('col-ready', 'Ready', story.new.id, bug.triaged.id), wipLimit: 8, split: true },
        column('col-active', 'In progress', story.active.id, bug.active.id),
        column('col-review', 'In review', story.active.id, bug.active.id),
        column('col-done', 'Done', story.closed.id, bug.closed.id),
      ],
    },
  });
}

interface StubbedRoute {
  request(): { method(): string; postDataJSON(): unknown };
  fulfill(response: { json: unknown; status?: number }): Promise<void>;
}
interface Routable {
  route(url: string, handler: (route: StubbedRoute) => Promise<void>): Promise<unknown>;
}

async function stubBoard(
  page: Routable,
  dto: BoardSettingsDto,
  refusal?: string,
  boardStatus = 200,
): Promise<BoardSettingsPut[]> {
  const puts: BoardSettingsPut[] = [];
  // The board read behind the delete count: no column holds a card unless a test says the read fails.
  await page.route(`**/api/boards/${BOARD_ID}`, (route) =>
    boardStatus === 200
      ? route.fulfill({
          json: {
            boardId: BOARD_ID,
            columns: dto.settings.columns.map((c) => ({ id: c.id, count: 0 })),
          },
        })
      : route.fulfill({ status: boardStatus, json: { error: 'boom', code: 'server' } }),
  );
  await page.route(`**/api/projects/${PROJECT_ID}/boards`, (route) =>
    route.fulfill({ json: [{ id: BOARD_ID, level: dto.level, name: dto.level }] }),
  );
  await page.route(`**/api/boards/${BOARD_ID}/settings`, async (route) => {
    if (route.request().method() === 'GET') return route.fulfill({ json: dto });
    const body = route.request().postDataJSON() as BoardSettingsPut;
    puts.push(body);
    if (refusal !== undefined)
      return route.fulfill({ status: 400, json: { error: refusal, code: 'validation' } });
    return route.fulfill({
      json: { board: { ...dto, settings: body.settings }, mergedSections: [] },
    });
  });
  return puts;
}

const mountColumns = (
  mount: (component: React.JSX.Element) => Promise<unknown>,
  level: 'stories' | 'features' = 'stories',
) =>
  mount(
    <BoardSettingsDialog
      projectId={PROJECT_ID}
      level={level}
      section="columns"
      onSectionChange={() => undefined}
      onClose={() => undefined}
    />,
  );

test('ColumnsPane_WhenStateChosen_ShouldPutTheMapping', async ({ mount, page }) => {
  const puts = await stubBoard(page, storiesBoard());
  await mountColumns(mount);

  await page.getByTestId(ids.columnStateBug).nth(1).selectOption({ label: 'New' });
  await page.getByTestId(dialogIds.save).click();

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.columns[1].stateMappings).toContainEqual({
    type: 'bug',
    stateId: 'b-new',
  });
  expect(puts[0].settings.columns[1].stateMappings).toHaveLength(2);
});

test('ColumnsPane_WhenWipSet_ShouldPutItAndBlankMeansNone', async ({ mount, page }) => {
  const puts = await stubBoard(page, storiesBoard());
  await mountColumns(mount);

  await page.getByTestId(ids.columnWip).first().fill('3');
  await page.getByTestId(ids.columnWip).nth(1).fill('');
  await page.getByTestId(dialogIds.save).click();

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.columns.map((c) => c.wipLimit)).toEqual([null, 3, null, null, null]);
});

test('ColumnsPane_WhenFirstAndLast_ShouldHaveNoWipOrSplit', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard());
  await mountColumns(mount);

  await expect(page.getByTestId(ids.columnWip)).toHaveCount(3);
  await expect(page.getByTestId(ids.columnSplit)).toHaveCount(3);
  await expect(page.getByTestId(ids.columnDelete).first()).toBeDisabled();
  await expect(page.getByTestId(ids.columnDelete).last()).toBeDisabled();
  await expect(page.getByTestId(ids.columnGrip).first()).toBeDisabled();
});

test('ColumnsPane_WhenSplitToggled_ShouldPutIt', async ({ mount, page }) => {
  const puts = await stubBoard(page, storiesBoard());
  await mountColumns(mount);

  await page.getByTestId(ids.columnSplit).first().uncheck();
  await page.getByTestId(ids.columnSplit).nth(1).check();
  await page.getByTestId(dialogIds.save).click();

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.columns.map((c) => c.split)).toEqual([false, false, true, false, false]);
});

test('ColumnsPane_WhenDefinitionOfDoneTyped_ShouldPutIt', async ({ mount, page }) => {
  const puts = await stubBoard(page, storiesBoard());
  await mountColumns(mount);

  await page.getByTestId(ids.columnDodToggle).nth(1).click();
  await page.getByTestId(ids.columnDodText).fill('- Estimated');
  await page.getByTestId(dialogIds.save).click();

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.columns[1].definitionOfDone).toBe('- Estimated');
});

test('ColumnsPane_WhenFeaturesBoard_ShouldShowOneStateColumn', async ({ mount, page }) => {
  const feature = [
    { id: 'f-new', name: 'New', category: 'proposed' },
    { id: 'f-done', name: 'Closed', category: 'completed' },
  ] as const;
  await stubBoard(
    page,
    boardSettingsDto({
      level: 'features',
      states: [{ type: 'feature', states: [...feature] }],
      settings: {
        ...emptySettings(),
        columns: [
          {
            ...column('c1', 'New', '', ''),
            stateMappings: [{ type: 'feature', stateId: 'f-new' }],
          },
          {
            ...column('c2', 'Done', '', ''),
            stateMappings: [{ type: 'feature', stateId: 'f-done' }],
          },
        ],
      },
    }),
  );
  await mountColumns(mount, 'features');

  await expect(page.getByTestId(ids.columnStateFeature)).toHaveCount(2);
  await expect(page.getByTestId(ids.columnStateStory)).toHaveCount(0);
  await expect(page.getByTestId(ids.columnsTypeCount)).toHaveCount(1);
  await expect(page.getByTestId(ids.columnsAllMapped)).toBeVisible();
});

test('ColumnsPane_WhenStateUnmapped_ShouldWarnAndFixInOneClick', async ({ mount, page }) => {
  await stubBoard(page, storiesBoard());
  await mountColumns(mount);
  await expect(page.getByTestId(ids.columnsAllMapped)).toBeVisible();

  await page.getByTestId(ids.columnStateBug).nth(1).selectOption({ label: 'New' });

  await expect(page.getByTestId(ids.columnsUnmapped)).toContainText(
    "Bug state Triaged isn't on any column",
  );
  await expect(page.getByTestId(ids.columnsFix)).toHaveText('Map Ready to Triaged for bugs');
  await page.getByTestId(ids.columnsFix).click();
  await expect(page.getByTestId(ids.columnsUnmapped)).toHaveCount(0);
  await expect(page.getByTestId(ids.columnStateBug).nth(1)).toHaveValue('b-triaged');
});

test('ColumnsPane_WhenNoColumnCanTakeTheState_ShouldOfferANewColumn', async ({ mount, page }) => {
  const dto = storiesBoard();
  dto.settings.columns = dto.settings.columns.filter((c) => c.id !== 'col-ready');
  await stubBoard(page, dto);
  await mountColumns(mount);

  await page.getByTestId(ids.columnsFix).click();

  await expect(page.getByTestId(ids.columnRow)).toHaveCount(5);
  await expect(page.getByTestId(ids.columnName).nth(3)).toHaveValue('Triaged');
  await expect(page.getByTestId(ids.columnsUnmapped)).toHaveCount(0);
});

test('ColumnsPane_WhenColumnAdded_ShouldGoBeforeTheLast', async ({ mount, page }) => {
  const puts = await stubBoard(page, storiesBoard());
  await mountColumns(mount);

  await page.getByTestId(ids.columnsAdd).click();
  await page.getByTestId(dialogIds.save).click();

  await expect.poll(() => puts.length).toBe(1);
  const names = puts[0].settings.columns.map((c) => c.name);
  expect(names).toEqual(['New', 'Ready', 'In progress', 'In review', 'New column', 'Done']);
  expect(puts[0].settings.columns[4].id).toMatch(/^col-/);
});

test('ColumnsPane_WhenColumnDeleted_ShouldDropItFromTheDraftAndShowTheServersRefusal', async ({
  mount,
  page,
}) => {
  const puts = await stubBoard(
    page,
    storiesBoard(),
    'Column Ready still has cards. Move them first.',
  );
  await mountColumns(mount);

  await page.getByTestId(ids.columnDelete).nth(1).click();
  await expect(page.getByTestId(ids.columnRow)).toHaveCount(4);
  await page.getByTestId(dialogIds.save).click();

  await expect.poll(() => puts.length).toBe(1);
  await expect(page.getByTestId(dialogIds.footerError)).toContainText(
    'Column Ready still has cards. Move them first.',
  );
  await expect(page.getByTestId(ids.columnDeleted)).toContainText('Ready');

  await page.getByTestId(ids.columnUndoDelete).click();

  await expect(page.getByTestId(ids.columnDeleted)).toHaveCount(0);
  await expect(page.getByTestId(ids.columnName).nth(1)).toHaveValue('Ready');
});

test('ColumnsPane_WhenBoardCannotSayHowManyCards_ShouldStillDeleteTheColumn', async ({
  mount,
  page,
}) => {
  const puts = await stubBoard(page, storiesBoard(), undefined, 500);
  await mountColumns(mount);

  await page.getByTestId(ids.columnDelete).nth(1).click();

  await expect(page.getByTestId(ids.columnRow)).toHaveCount(4);
  await expect(page.getByTestId(ids.columnBlocked)).toHaveCount(0);
  await page.getByTestId(dialogIds.save).click();
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.columns.map((c) => c.id)).not.toContain('col-ready');
});

test('ColumnsPane_WhenColumnMapsARemovedState_ShouldFlagItsRow', async ({ mount, page }) => {
  const dto = storiesBoard();
  dto.settings.columns[2] = column('col-active', 'In progress', story.removed.id, bug.active.id);
  await stubBoard(page, dto);
  await mountColumns(mount);

  const select = page.getByTestId(ids.columnStateStory).nth(2);
  await expect(select).toHaveAttribute('aria-invalid', 'true');
  await expect(select.locator('option:checked')).toHaveText('Removed state — pick one');
  await select.selectOption({ label: 'Active' });
  await expect(select).toHaveAttribute('aria-invalid', 'false');
});

test('ColumnsPane_WhenDefinitionOfDoneTyped_ShouldPreviewTheServersRender', async ({
  mount,
  page,
}) => {
  await stubBoard(page, storiesBoard());
  await page.route('**/api/markdown/render', (route) => {
    const { markdown } = route.request().postDataJSON() as { markdown: string };
    return route.fulfill({ json: { markdown, html: `<p>rendered:${markdown}</p>` } });
  });
  await mountColumns(mount);

  await page.getByTestId(ids.columnDodToggle).nth(1).click();
  await expect(page.getByTestId(ids.columnDodPreview)).toContainText('Nothing yet');
  await page.getByTestId(ids.columnDodText).fill('- Estimated');

  await expect(page.getByTestId(ids.columnDodPreview)).toContainText('rendered:- Estimated');
});

test('ColumnsPane_WhenAltArrowPressedOnGrip_ShouldMoveOnlyMiddleColumns', async ({
  mount,
  page,
}) => {
  const puts = await stubBoard(page, storiesBoard());
  await mountColumns(mount);
  const names = () => page.getByTestId(ids.columnName);

  await page.getByTestId(ids.columnGrip).nth(1).focus();
  await page.keyboard.press('Alt+ArrowDown');
  await expect(names().nth(2)).toHaveValue('Ready');
  await expect(page.getByTestId(ids.columnGrip).nth(2)).toBeFocused();

  await page.keyboard.press('Alt+ArrowDown');
  await page.keyboard.press('Alt+ArrowDown');
  await expect(names().nth(3)).toHaveValue('Ready');
  await expect(names().first()).toHaveValue('New');
  await expect(names().last()).toHaveValue('Done');

  await page.getByTestId(dialogIds.save).click();
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.columns.map((c) => c.id)).toEqual([
    'col-new',
    'col-active',
    'col-review',
    'col-ready',
    'col-done',
  ]);
});
