import { expect, test } from '@playwright/experimental-ct-react';
import type { BoardSettingsDto, BoardSettingsPut } from '../../api/boards';
import { componentTestIds } from '../../components/testIds';
import { settingsDialogTestIds as ids } from '../settingsDialog/testIds';
import { BoardSettingsDialog } from './BoardSettingsDialog';
import { BoardSettingsHarness } from './ct/BoardSettingsHarness';
import { boardHarnessIds } from './ct/harnessIds';
import { BOARD_ID, boardSettingsDto, emptySettings, PROJECT_ID } from './ct/boardSettingsFixtures';
import { boardSettingsTestIds } from './testIds';

const settingsUrl = `**/api/boards/${BOARD_ID}/settings`;
const boardsUrl = `**/api/projects/${PROJECT_ID}/boards`;

interface StubbedRoute {
  request(): { method(): string; postDataJSON(): unknown };
  fulfill(response: { json: unknown; status?: number }): Promise<void>;
}
interface Routable {
  route(url: string, handler: (route: StubbedRoute) => Promise<void>): Promise<unknown>;
  on(event: 'pageerror', handler: (error: Error) => void): unknown;
}

interface SettingsServer {
  puts: BoardSettingsPut[];
  gets(): number;
}

// Stubs the boards list and the board's settings: GET answers `loaded(n)` (n = which GET this is), a PUT answers
// `onPut(body, n)` (a response with a status, or the default: the draft taken as the board's settings).
async function stubBoard(
  page: Routable,
  options: {
    loaded?: (n: number) => BoardSettingsDto;
    onPut?: (body: BoardSettingsPut, n: number) => { status: number; json: unknown } | undefined;
    merged?: string[];
  } = {},
): Promise<SettingsServer> {
  const puts: BoardSettingsPut[] = [];
  let gets = 0;
  await page.route(boardsUrl, (route) =>
    route.fulfill({ json: [{ id: BOARD_ID, level: 'stories', name: 'Stories' }] }),
  );
  await page.route(settingsUrl, async (route) => {
    if (route.request().method() === 'GET') {
      gets += 1;
      await route.fulfill({ json: (options.loaded ?? (() => boardSettingsDto()))(gets) });
      return;
    }
    const body = route.request().postDataJSON() as BoardSettingsPut;
    puts.push(body);
    const answer = options.onPut?.(body, puts.length);
    if (answer !== undefined) {
      await route.fulfill(answer);
      return;
    }
    await route.fulfill({
      json: {
        board: boardSettingsDto({ settings: body.settings }),
        mergedSections: options.merged ?? [],
      },
    });
  });
  return { puts, gets: () => gets };
}

function mountDialog(mount: (component: React.JSX.Element) => Promise<unknown>, section?: string) {
  return mount(
    <BoardSettingsDialog
      projectId={PROJECT_ID}
      level="stories"
      section={section}
      onSectionChange={() => undefined}
      onClose={() => undefined}
    />,
  );
}

function mountHarness(
  mount: (component: React.JSX.Element) => Promise<unknown>,
  apply: 'onSave' | 'immediately',
) {
  return mount(<BoardSettingsHarness projectId={PROJECT_ID} boardId={BOARD_ID} apply={apply} />);
}

const theirSwimlanes = [{ id: 'l1', name: 'Theirs' }] as never;

test('BoardSettingsDialog_WhenLoaded_ShouldOpenColumnsByDefault', async ({ mount, page }) => {
  await stubBoard(page);
  await mountDialog(mount);
  await expect(page.getByTestId(boardSettingsTestIds.columnsPane)).toBeAttached();
  await expect(page.getByTestId(componentTestIds.dialog)).toHaveCSS('width', '1080px');
});

test('BoardSettingsDialog_WhenLevelGiven_ShouldTitleItAndFindItsBoard', async ({ mount, page }) => {
  await page.route(boardsUrl, (route) =>
    route.fulfill({ json: [{ id: BOARD_ID, level: 'epics', name: 'Epics' }] }),
  );
  await page.route(settingsUrl, (route) =>
    route.fulfill({ json: boardSettingsDto({ level: 'epics' }) }),
  );
  await mount(
    <BoardSettingsDialog
      projectId={PROJECT_ID}
      level="epics"
      section={undefined}
      onSectionChange={() => undefined}
      onClose={() => undefined}
    />,
  );
  await expect(page.getByRole('dialog', { name: 'Epics board settings' })).toBeVisible();
  await expect(page.getByTestId(boardSettingsTestIds.columnsPane)).toBeAttached();
});

test('BoardSettingsDialog_WhenNoBoardOfTheLevel_ShouldSayItFailed', async ({ mount, page }) => {
  await page.route(boardsUrl, (route) => route.fulfill({ json: [] }));
  await mountDialog(mount);
  await expect(page.getByRole('dialog', { name: 'Stories board settings' })).toBeVisible();
  await expect(page.getByTestId(boardSettingsTestIds.loadFailed)).toBeVisible();
});

test('BoardSettingsDialog_WhenSectionGiven_ShouldOpenThatPane', async ({ mount, page }) => {
  await stubBoard(page);
  await mountDialog(mount, 'tags');
  await expect(page.getByTestId(boardSettingsTestIds.tagsPane)).toBeAttached();
});

test('BoardSettingsDialog_WhenSectionUnknown_ShouldOpenColumns', async ({ mount, page }) => {
  await stubBoard(page);
  await mountDialog(mount, 'bogus');
  await expect(page.getByTestId(boardSettingsTestIds.columnsPane)).toBeAttached();
  await expect(page.getByTestId(boardSettingsTestIds.fieldsPane)).toHaveCount(0);
});

test('BoardSettingsDialog_WhenAStateIsOnNoColumn_ShouldMarkColumnsAndFooter', async ({
  mount,
  page,
}) => {
  const dto = boardSettingsDto({
    states: [
      {
        type: 'story',
        states: [
          { id: 's1', name: 'Blocked', category: 'inProgress' },
          { id: 's2', name: 'Gone', category: 'removed' },
        ],
      },
    ],
  });
  await stubBoard(page, { loaded: () => dto });
  await mountDialog(mount);
  await expect(page.getByTestId(`${ids.navWarning}-columns`)).toBeVisible();
  await expect(page.getByTestId(ids.paneWarning)).toHaveText('1 state not on any column');
  await expect(page.getByTestId(`${ids.footerWarning}-columns`)).toHaveText(
    '1 state not on any column',
  );
});

test('BoardSettingsDialog_WhenPaneWarnsFromTheDraft_ShouldShowAChipInTheFooter', async ({
  mount,
  page,
}) => {
  await stubBoard(page);
  await mountHarness(mount, 'onSave');
  await expect(page.getByTestId(`${ids.footerWarning}-annotations`)).toHaveCount(0);
  await page.getByTestId(boardHarnessIds.toggleTasks).click();
  await expect(page.getByTestId(`${ids.footerWarning}-annotations`)).toHaveText(
    '1 annotation hidden',
  );
  await page.getByTestId(boardHarnessIds.toggleTasks).click();
  await expect(page.getByTestId(`${ids.footerWarning}-annotations`)).toHaveCount(0);
});

test('BoardSettingsDialog_WhenSaved_ShouldPutBasedOnAndDraft', async ({ mount, page }) => {
  const server = await stubBoard(page);
  await mountHarness(mount, 'onSave');
  await page.getByTestId(boardHarnessIds.toggleTasks).click();
  await expect(page.getByTestId(`${ids.navDot}-annotations`)).toBeVisible();
  await page.getByTestId(ids.save).click();

  // Save closes the dialog and says so
  await expect(page.getByTestId(boardHarnessIds.closed)).toBeAttached();
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('Board settings saved');
  expect(server.puts).toHaveLength(1);
  expect(server.puts[0]?.basedOn).toEqual(emptySettings());
  expect(server.puts[0]?.settings).toEqual({
    ...emptySettings(),
    annotations: { ...emptySettings().annotations, tasks: false },
  });
});

test('BoardSettingsDialog_WhenValidationError_ShouldShowItAndStayOpen', async ({ mount, page }) => {
  await stubBoard(page, {
    onPut: () => ({
      status: 400,
      json: { error: 'Every column needs a state mapping.', code: 'validation' },
    }),
  });
  await mountHarness(mount, 'onSave');
  await page.getByTestId(boardHarnessIds.toggleTasks).click();
  await page.getByTestId(ids.save).click();

  await expect(page.getByTestId(ids.footerError)).toHaveText('Every column needs a state mapping.');
  await expect(page.getByTestId(componentTestIds.dialog)).toBeVisible();
  await expect(page.getByTestId(`${ids.navDot}-annotations`)).toBeVisible();
  // a refusal resending cannot fix has no Retry
  await expect(page.getByTestId(ids.footerRetry)).toHaveCount(0);
});

test('BoardSettingsDialog_WhenRefetchedUnderADraft_ShouldKeepTheTeammatesSectionsOnSave', async ({
  mount,
  page,
}) => {
  const theirs = { ...emptySettings(), swimlanes: theirSwimlanes };
  const server = await stubBoard(page, {
    loaded: (n) => boardSettingsDto(n === 1 ? {} : { settings: theirs }),
    merged: [],
  });
  await mountHarness(mount, 'onSave');
  await page.getByTestId(boardHarnessIds.toggleTasks).click();
  // the window regains focus: react-query refetches while the draft is open
  await page.evaluate(() => window.dispatchEvent(new Event('visibilitychange')));
  await expect.poll(() => server.gets()).toBe(2);
  // the Swimlanes mark would be one the user never caused
  await expect(page.getByTestId(`${ids.navDot}-annotations`)).toBeVisible();
  await expect(page.getByTestId(`${ids.navDot}-swimlanes`)).toHaveCount(0);
  await expect(page.getByTestId(ids.footerDirty)).not.toContainText('Swimlanes');
  await page.getByTestId(ids.save).click();

  await expect.poll(() => server.puts.length).toBe(1);
  expect(server.puts[0]?.basedOn.swimlanes).toEqual(theirSwimlanes);
  expect(server.puts[0]?.settings.swimlanes).toEqual(theirSwimlanes);
  expect(server.puts[0]?.settings.annotations.tasks).toBe(false);
});

test('BoardSettingsDialog_WhenTheServerMergedSections_ShouldToastThem', async ({ mount, page }) => {
  await stubBoard(page, { merged: ['swimlanes'] });
  await mountHarness(mount, 'onSave');
  await page.getByTestId(boardHarnessIds.toggleTasks).click();
  await page.getByTestId(ids.save).click();
  await expect(page.getByTestId(componentTestIds.toast).first()).toContainText('Swimlanes');
});

for (const failure of [
  { status: 409, code: 'conflict' },
  { status: 500, code: 'internal_error' },
  { status: 403, code: 'forbidden' },
]) {
  test(`BoardSettingsDialog_WhenSaveFailsWith${failure.status}_ShouldShowItAndRetry`, async ({
    mount,
    page,
  }) => {
    const pageErrors: string[] = [];
    page.on('pageerror', (error) => pageErrors.push(error.message));
    const server = await stubBoard(page, {
      onPut: (_body, n) =>
        n === 1
          ? {
              status: failure.status,
              json: { error: `Boom ${failure.status}`, code: failure.code },
            }
          : undefined,
    });
    await mountHarness(mount, 'onSave');
    await page.getByTestId(boardHarnessIds.toggleTasks).click();
    await page.getByTestId(ids.save).click();

    await expect(page.getByTestId(ids.footerError)).toHaveText(`Boom ${failure.status}`);
    await expect(page.getByTestId(componentTestIds.toast)).toContainText(`Boom ${failure.status}`);
    await expect(page.getByTestId(componentTestIds.dialog)).toBeVisible();
    await expect(page.getByTestId(`${ids.navDot}-annotations`)).toBeVisible();

    await page.getByTestId(ids.footerRetry).click();
    await expect(page.getByTestId(boardHarnessIds.closed)).toBeAttached();
    expect(server.puts).toHaveLength(2);
    expect(pageErrors).toEqual([]);
  });
}

test('BoardSettingsDialog_WhenImmediate_ShouldChainBasedOnThroughTheQueryCache', async ({
  mount,
  page,
}) => {
  const theirs = { ...emptySettings(), swimlanes: theirSwimlanes };
  // the first PUT comes back carrying a teammate's swimlanes, merged in
  const server = await stubBoard(page, {
    onPut: (body, n) =>
      n === 1
        ? {
            status: 200,
            json: {
              board: boardSettingsDto({
                settings: { ...body.settings, swimlanes: theirs.swimlanes },
              }),
              mergedSections: ['swimlanes'],
            },
          }
        : undefined,
  });
  await mountHarness(mount, 'immediately');
  await expect(page.getByTestId(ids.immediateNote)).toBeVisible();
  await page.getByTestId(boardHarnessIds.toggleTasks).click();
  await expect(page.getByTestId(componentTestIds.toast).first()).toContainText('Swimlanes');
  await page.getByTestId(boardHarnessIds.toggleTasks).click();

  await expect.poll(() => server.puts.length).toBe(2);
  expect(server.puts[0]?.basedOn).toEqual(emptySettings());
  // based on what the first PUT returned, not on what was loaded
  expect(server.puts[1]?.basedOn.swimlanes).toEqual(theirSwimlanes);
  expect(server.puts[1]?.basedOn.annotations.tasks).toBe(false);
  expect(server.puts[1]?.settings.swimlanes).toEqual(theirSwimlanes);
  expect(server.puts[1]?.settings.annotations.tasks).toBe(true);
  await expect(page.getByTestId(ids.save)).toHaveCount(0);
});

test('BoardSettingsDialog_WhenImmediateAndRefetchedUnderAChange_ShouldSendTheRefetchedBase', async ({
  mount,
  page,
}) => {
  const theirs = { ...emptySettings(), swimlanes: theirSwimlanes };
  const server = await stubBoard(page, {
    loaded: (n) => boardSettingsDto(n === 1 ? {} : { settings: theirs }),
    // the first PUT fails, so the change is still unconfirmed when the refetch lands
    onPut: (_body, n) =>
      n === 1 ? { status: 500, json: { error: 'Boom', code: 'internal_error' } } : undefined,
  });
  await mountHarness(mount, 'immediately');
  await page.getByTestId(boardHarnessIds.toggleTasks).click();
  await expect(page.getByTestId(ids.footerError)).toBeVisible();
  await page.evaluate(() => window.dispatchEvent(new Event('visibilitychange')));
  await expect.poll(() => server.gets()).toBe(2);
  await expect(page.getByTestId(`${ids.navDot}-swimlanes`)).toHaveCount(0);
  await page.getByTestId(ids.footerRetry).click();

  await expect.poll(() => server.puts.length).toBe(2);
  expect(server.puts[1]?.basedOn.swimlanes).toEqual(theirSwimlanes);
  expect(server.puts[1]?.settings.swimlanes).toEqual(theirSwimlanes);
  expect(server.puts[1]?.settings.annotations.tasks).toBe(false);
});
