import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectSettingsDto, ProjectSettingsPut, StateDto } from '../../../api/projects';
import { componentTestIds } from '../../../components/testIds';
import { PROJECT_ID } from '../draft/ct/projectDto';
import { StatesHarness } from '../ct/StatesHarness';
import { settingsTestIds as ids } from '../testIds';

const joel = {
  id: 'u-joel',
  name: 'Joel Dickson',
  initials: 'JD',
  avatarColour: '#5b6b8c',
  photoUrl: null,
};

const st = (
  id: string,
  name: string,
  category: StateDto['category'],
  order: number,
  itemCount = 0,
): StateDto => ({ id, type: 'story', name, category, order, itemCount });

const otherTypes = (): StateDto[] =>
  (['epic', 'feature', 'bug', 'task'] as const).flatMap((type) => [
    {
      id: `${type}-new`,
      type,
      name: 'New',
      category: 'proposed' as const,
      order: 1,
      itemCount: 0,
    },
    {
      id: `${type}-done`,
      type,
      name: 'Done',
      category: 'completed' as const,
      order: 2,
      itemCount: 0,
    },
  ]);

const saved = (): ProjectSettingsDto => ({
  general: {
    key: 'SHIP',
    name: 'Storefront',
    description: '',
    workingDays: [1, 2, 3, 4, 5],
    agentConnectionId: null,
    repoUrl: null,
    repoBaseBranch: null,
    agentConnections: [],
  },
  members: [{ user: joel, email: 'joel@shiplean.local', role: 'admin', status: 'active' }],
  sprints: { velocityWindow: 3, velocityOverride: null },
  states: [
    st('s-new', 'New', 'proposed', 1, 0),
    st('s-active', 'Active', 'inProgress', 2, 3),
    st('s-closed', 'Closed', 'completed', 3, 4),
    ...otherTypes(),
  ],
});

const storiesBoard = {
  id: 'b-stories',
  projectId: PROJECT_ID,
  level: 'stories',
  settings: {
    columns: [
      {
        id: 'c1',
        name: 'Backlog',
        wipLimit: null,
        split: false,
        definitionOfDone: '',
        stateMappings: [{ type: 'story', stateId: 's-new' }],
      },
      {
        id: 'c2',
        name: 'Doing',
        wipLimit: null,
        split: false,
        definitionOfDone: '',
        stateMappings: [{ type: 'story', stateId: 's-active' }],
      },
    ],
  },
  updatedAt: '',
  states: [],
  warnings: [],
};

interface Routable {
  route(
    url: string,
    handler: (route: {
      request(): { method(): string; postDataJSON(): unknown };
      fulfill(response: { json: unknown; status?: number }): Promise<void>;
    }) => Promise<void>,
  ): Promise<unknown>;
}

const stub = async (page: Routable, settings: ProjectSettingsDto = saved()) => {
  const puts: ProjectSettingsPut[] = [];
  await page.route(`**/api/projects/${PROJECT_ID}/settings`, async (route) => {
    if (route.request().method() === 'PUT')
      puts.push(route.request().postDataJSON() as ProjectSettingsPut);
    await route.fulfill({ json: settings });
  });
  await page.route(`**/api/projects/${PROJECT_ID}/boards`, (route) =>
    route.fulfill({ json: [{ id: 'b-stories', level: 'stories', name: 'Stories' }] }),
  );
  await page.route('**/api/boards/b-stories/settings', (route) =>
    route.fulfill({ json: storiesBoard }),
  );
  return puts;
};

const mountAdmin = async (mount: Parameters<Parameters<typeof test>[2]>[0]['mount']) =>
  mount(<StatesHarness role="admin" />, { hooksConfig: { route: '/p/SHIP/settings/states' } });

test('States_WhenStateAdded_ShouldAppearAndStageAnAdd', async ({ mount, page }) => {
  const puts = await stub(page);
  const view = await mountAdmin(mount);
  await expect(view.getByTestId(ids.stateRow)).toHaveCount(3);

  await view.getByTestId(ids.stateAddName).fill('In review');
  await view.getByTestId(ids.stateAddCategory).selectOption('inProgress');
  await view.getByTestId(ids.stateAdd).click();

  await expect(view.getByTestId(ids.stateRow)).toHaveCount(4);
  await expect(view.getByTestId(ids.unsaved)).toContainText('1 unsaved change');
  await expect(view.getByTestId(ids.tabDirty)).toHaveCount(1);
  await view.getByTestId(ids.save).click();
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0]?.states.add).toMatchObject([
    { type: 'story', name: 'In review', category: 'inProgress' },
  ]);
  expect(puts[0]?.states.add[0]?.id).toMatch(/^[0-9a-f-]{36}$/);
});

test('States_WhenStateRenamed_ShouldKeepCategoryColour', async ({ mount, page }) => {
  const puts = await stub(page);
  const view = await mountAdmin(mount);
  const row = view.getByTestId(ids.stateRow).nth(1);
  await expect(row.getByTestId('state-dot')).toHaveAttribute('data-category', 'inProgress');

  await row.getByTestId(ids.stateName).fill('Doing');

  await expect(row.getByTestId('state-dot')).toHaveAttribute('data-category', 'inProgress');
  await expect(row.getByTestId(ids.stateCategory)).toHaveValue('inProgress');
  await view.getByTestId(ids.save).click();
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0]?.states.update).toEqual([
    { stateId: 's-active', name: 'Doing', category: 'inProgress' },
  ]);
});

test('States_WhenReordered_ShouldSendTheFullOrder', async ({ mount, page }) => {
  const puts = await stub(page);
  const view = await mountAdmin(mount);

  await view.getByTestId(ids.stateRow).nth(0).getByTestId(ids.stateMoveDown).click();

  await expect(view.getByTestId(ids.stateName).first()).toHaveValue('Active');
  await view.getByTestId(ids.save).click();
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0]?.states.reorder).toEqual([
    { type: 'story', stateIds: ['s-active', 's-new', 's-closed'] },
  ]);
});

test('States_WhenDeletedWithItems_ShouldAskTargetAndSendTheMove', async ({ mount, page }) => {
  const puts = await stub(page);
  const view = await mountAdmin(mount);

  await view.getByTestId(ids.stateRow).nth(1).getByTestId(ids.stateDelete).click();
  await expect(view.getByTestId(ids.stateDeleteForm)).toContainText('3 work items move');
  await expect(view.getByTestId(ids.stateDeleteTarget)).toHaveValue('s-new');
  await view.getByTestId(ids.stateDeleteTarget).selectOption('s-closed');
  await view.getByTestId(ids.stateDeleteTarget).selectOption('s-new');
  await view.getByTestId(ids.stateDeleteConfirm).click();

  await expect(view.getByTestId(ids.stateRow)).toHaveCount(2);
  await expect(view.getByTestId(ids.stateRow).first().getByTestId(ids.stateItems)).toContainText(
    '3',
  );
  expect(puts).toHaveLength(0);
  await view.getByTestId(ids.save).click();
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0]?.states.remove).toEqual([{ stateId: 's-active', targetStateId: 's-new' }]);
});

test('States_WhenDeletedWithoutItems_ShouldNotAsk', async ({ mount, page }) => {
  const puts = await stub(page);
  const view = await mountAdmin(mount);
  await view.getByTestId(ids.stateAddName).fill('Spare');
  await view.getByTestId(ids.stateAdd).click();

  await view.getByTestId(ids.stateRow).nth(3).getByTestId(ids.stateDelete).click();

  await expect(view.getByTestId(ids.stateDeleteForm)).toHaveCount(0);
  await expect(view.getByTestId(ids.stateRow)).toHaveCount(3);
  await expect(view.getByTestId(ids.unsaved)).toHaveCount(0);
  expect(puts).toHaveLength(0);
});

test('States_WhenLastOfARequiredCategory_ShouldNotDelete', async ({ mount, page }) => {
  await stub(page);
  const view = await mountAdmin(mount);
  const closed = view.getByTestId(ids.stateRow).nth(2);

  await expect(closed.getByTestId(ids.stateDelete)).toBeDisabled();
  await expect(closed.getByTestId(ids.stateDeleteBlock)).toContainText('last Completed');
  await expect(view.getByTestId(ids.stateRow).nth(0).getByTestId(ids.stateDelete)).toBeDisabled();
  await expect(view.getByTestId(ids.stateRow).nth(1).getByTestId(ids.stateDelete)).toBeEnabled();
});

test('States_WhenMember_ShouldBeReadOnly', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<StatesHarness role="member" />, {
    hooksConfig: { route: '/p/SHIP/settings/states' },
  });
  await expect(view.getByTestId(ids.stateRow)).toHaveCount(3);
  await expect(view.getByTestId(ids.stateName).first()).toBeDisabled();
  await expect(view.getByTestId(ids.stateCategory).first()).toBeDisabled();
  await expect(view.getByTestId(ids.stateAdd)).toHaveCount(0);
  await expect(view.getByTestId(ids.stateDelete)).toHaveCount(0);
  await expect(view.getByTestId(ids.stateMoveUp)).toHaveCount(0);
});

test('States_WhenEditedWithOtherTabs_ShouldGoOutInTheOnePut', async ({ mount, page }) => {
  const puts = await stub(page);
  const view = await mountAdmin(mount);

  await view.getByTestId(ids.stateRow).nth(1).getByTestId(ids.stateName).fill('Doing');
  await view.getByTestId(ids.tabSprints).click();
  await view.getByTestId(ids.velocityWindow).fill('4');
  await view.getByTestId(ids.tabStates).click();
  await expect(view.getByTestId(ids.stateRow).nth(1).getByTestId(ids.stateName)).toHaveValue(
    'Doing',
  );
  await expect(view.getByTestId(ids.unsaved)).toContainText('2 unsaved changes');

  await view.getByTestId(ids.save).click();
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0]?.sprints.velocityWindow).toBe(4);
  expect(puts[0]?.states.update).toHaveLength(1);
});

test('States_WhenCompletedRecategorised_ShouldShowTheTypeError', async ({ mount, page }) => {
  await stub(page);
  const view = await mountAdmin(mount);
  await view
    .getByTestId(ids.stateRow)
    .nth(2)
    .getByTestId(ids.stateCategory)
    .selectOption('resolved');
  await expect(view.getByTestId(ids.stateTypeError)).toContainText('Completed');
});

test('States_WhenDeletedIntoAStateAddedInTheDraft_ShouldSendTheNewIdAndSave', async ({
  mount,
  page,
}) => {
  const puts = await stub(page);
  const view = await mountAdmin(mount);
  await view.getByTestId(ids.stateAddName).fill('Doing');
  await view.getByTestId(ids.stateAddCategory).selectOption('inProgress');
  await view.getByTestId(ids.stateAdd).click();

  await view.getByTestId(ids.stateRow).nth(1).getByTestId(ids.stateDelete).click();
  await expect(view.getByTestId(ids.stateDeleteTarget)).toHaveValue(/^[0-9a-f-]{36}$/);
  await view.getByTestId(ids.stateDeleteConfirm).click();

  await expect(view.getByTestId(componentTestIds.toastHost)).toContainText(
    'Active will be deleted on save',
  );
  await expect(view.getByTestId(componentTestIds.toastHost)).toContainText('3 items move to Doing');
  await view.getByTestId(ids.save).click();
  await expect.poll(() => puts.length).toBe(1);
  const fresh = puts[0]?.states.add[0]?.id;
  expect(fresh).toMatch(/^[0-9a-f-]{36}$/);
  expect(puts[0]?.states.remove).toEqual([{ stateId: 's-active', targetStateId: fresh }]);
});

test('States_WhenOnlyIncomingItemsRemain_ShouldStillAskWhereTheyGo', async ({ mount, page }) => {
  await stub(page);
  const view = await mountAdmin(mount);
  await view.getByTestId(ids.stateAddName).fill('Intake');
  await view.getByTestId(ids.stateAddCategory).selectOption('proposed');
  await view.getByTestId(ids.stateAdd).click();
  await view.getByTestId(ids.stateRow).nth(1).getByTestId(ids.stateDelete).click();
  await view.getByTestId(ids.stateDeleteTarget).selectOption('s-new');
  await view.getByTestId(ids.stateDeleteConfirm).click();

  await view.getByTestId(ids.stateRow).nth(0).getByTestId(ids.stateDelete).click();

  await expect(view.getByTestId(ids.stateDeleteForm)).toContainText('3 work items move');
});

test('States_WhenTypeLosesItsCompletedState_ShouldBlockSaveAndFocusThePicker', async ({
  mount,
  page,
}) => {
  const puts = await stub(page);
  const view = await mountAdmin(mount);
  await view
    .getByTestId(ids.stateRow)
    .nth(2)
    .getByTestId(ids.stateCategory)
    .selectOption('resolved');

  await view.getByTestId(ids.save).click();

  await expect(view.getByTestId(ids.save)).toHaveText('Fix 1 error');
  await expect(view.getByTestId(ids.tabError)).toHaveCount(1);
  await expect(view.getByTestId(ids.stateTypeDot)).toHaveCount(1);
  await expect(view.getByTestId(ids.stateTypeStory)).toBeFocused();
  expect(puts).toHaveLength(0);
});

test('States_WhenAnotherTypeHasAProblem_ShouldShowThatTypeOnSave', async ({ mount, page }) => {
  const puts = await stub(page);
  const view = await mountAdmin(mount);
  await view.getByTestId(ids.stateTypeBug).click();
  await view
    .getByTestId(ids.stateRow)
    .nth(1)
    .getByTestId(ids.stateCategory)
    .selectOption('resolved');
  await view.getByTestId(ids.stateTypeStory).click();

  await view.getByTestId(ids.save).click();

  await expect(view.getByTestId(ids.stateTypeBug)).toHaveAttribute('aria-pressed', 'true');
  await expect(view.getByTestId(ids.stateTypeError)).toContainText('Completed');
  await expect(view.getByTestId(ids.stateTypeBug)).toBeFocused();
  expect(puts).toHaveLength(0);
});

test('States_WhenBlankNameOnSave_ShouldFocusTheRow', async ({ mount, page }) => {
  const puts = await stub(page);
  const view = await mountAdmin(mount);
  await view.getByTestId(ids.stateRow).nth(1).getByTestId(ids.stateName).fill(' ');

  await view.getByTestId(ids.save).click();

  await expect(view.getByTestId(ids.stateName).nth(1)).toBeFocused();
  expect(puts).toHaveLength(0);
});

test('States_WhenLoaded_ShouldShowStartChipAndBoardColumns', async ({ mount, page }) => {
  await stub(page);
  const view = await mountAdmin(mount);
  const rows = view.getByTestId(ids.stateRow);

  await expect(rows.nth(0).getByTestId(ids.stateStart)).toBeVisible();
  await expect(view.getByTestId(ids.stateStart)).toHaveCount(1);
  await expect(rows.nth(0).getByTestId(ids.stateColumn)).toHaveText('Backlog');
  await expect(rows.nth(1).getByTestId(ids.stateColumn)).toHaveText('Doing');
  await expect(rows.nth(2).getByTestId(ids.stateColumn)).toHaveText('Not mapped');
  await view.getByTestId(ids.stateTypeTask).click();
  await expect(rows.nth(0).getByTestId(ids.stateColumn)).toHaveText('New');
});

test('States_WhenStateAddedAndTypeEndsWithRemoved_ShouldGoBeforeRemoved', async ({
  mount,
  page,
}) => {
  const settings = saved();
  settings.states.push(st('s-removed', 'Removed', 'removed', 4));
  const puts = await stub(page, settings);
  const view = await mountAdmin(mount);
  await expect(view.getByTestId(ids.stateRow).nth(3).getByTestId(ids.stateColumn)).toHaveText(
    'Hidden (Removed)',
  );

  await view.getByTestId(ids.stateAddName).fill('In review');
  await view.getByTestId(ids.stateAddCategory).selectOption('inProgress');
  await view.getByTestId(ids.stateAdd).click();

  await expect(view.getByTestId(ids.stateName).nth(3)).toHaveValue('In review');
  await expect(view.getByTestId(ids.stateName).nth(4)).toHaveValue('Removed');
  await view.getByTestId(ids.save).click();
  await expect.poll(() => puts.length).toBe(1);
  const fresh = puts[0]?.states.add[0]?.id;
  expect(puts[0]?.states.reorder).toEqual([
    { type: 'story', stateIds: ['s-new', 's-active', 's-closed', fresh, 's-removed'] },
  ]);
});

test('States_WhenSavedWithItemsMovingToNewState_ShouldShowTheChangeAsSentence', async ({
  mount,
  page,
}) => {
  await stub(page);
  const view = await mountAdmin(mount);
  await view.getByTestId(ids.stateRow).nth(1).getByTestId(ids.stateDelete).click();
  await view.getByTestId(ids.stateDeleteConfirm).click();

  await view.getByTestId(ids.unsaved).click();

  await expect(view.getByTestId(ids.changeItem)).toHaveText(
    'User Story: deleted Active, 3 items move to New',
  );
});
