import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectSettingsDto, ProjectSettingsPut } from '../../../api/projects';
import { WORK_ITEM_TYPE_IDS } from '../../../api/refs';
import { settingsTestIds as ids } from '../testIds';
import { SettingsDraftHarness } from './ct/SettingsDraftHarness';
import { PROJECT_ID } from './ct/projectDto';

const joel = {
  id: 'u-joel',
  name: 'Joel Dickson',
  initials: 'JD',
  avatarColour: '#5b6b8c',
  photoUrl: null,
};
const priya = {
  id: 'u-priya',
  name: 'Priya Shah',
  initials: 'PS',
  avatarColour: '#2f7fd8',
  photoUrl: null,
};

const saved = (): ProjectSettingsDto => ({
  general: {
    key: 'SHIP',
    name: 'Storefront',
    description: 'Shop',
    workingDays: [1, 2, 3, 4, 5],
    agentConnectionId: null,
    repoUrl: null,
    repoBaseBranch: null,
    agentConnections: [{ id: 'c-1', name: 'Home relay', archived: false }],
  },
  members: [
    { user: joel, email: 'joel@shiplean.local', role: 'admin', status: 'active' },
    { user: priya, email: 'priya@shiplean.local', role: 'member', status: 'active' },
  ],
  sprints: { velocityWindow: 3, velocityOverride: null },
  states: WORK_ITEM_TYPE_IDS.flatMap((type) => [
    { id: `${type}-new`, type, name: 'New', category: 'proposed' as const, order: 1, itemCount: 0 },
    {
      id: `${type}-done`,
      type,
      name: 'Done',
      category: 'completed' as const,
      order: 2,
      itemCount: 0,
    },
  ]),
});

interface Routable {
  route(
    url: string,
    handler: (route: {
      request(): { method(): string; postDataJSON(): unknown };
      fulfill(response: { json: unknown; status?: number }): Promise<void>;
    }) => Promise<void>,
  ): Promise<unknown>;
}

const stub = async (
  page: Routable,
  onPut?: (body: ProjectSettingsPut) => { status: number; json: unknown },
) => {
  const puts: ProjectSettingsPut[] = [];
  await page.route(`**/api/projects/${PROJECT_ID}/settings`, async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({ json: saved() });
      return;
    }
    const body = route.request().postDataJSON() as ProjectSettingsPut;
    puts.push(body);
    const answer = onPut?.(body);
    if (answer !== undefined) {
      await route.fulfill(answer);
      return;
    }
    await route.fulfill({
      json: {
        ...saved(),
        general: { ...saved().general, ...body.general },
        sprints: body.sprints,
      },
    });
  });
  return puts;
};

test('Draft_WhenEditedAcrossTabs_ShouldMarkEachTabAndCountChanges', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<SettingsDraftHarness role="admin" />, {
    hooksConfig: { route: '/p/SHIP/settings/general' },
  });
  await view.getByTestId(ids.generalName).fill('Shop');
  await view.getByTestId(ids.tabSprints).click();
  await view.getByTestId(ids.velocityWindow).fill('4');
  await view.getByTestId(ids.tabMembers).click();

  await expect(view.getByTestId(ids.unsaved)).toContainText('2 unsaved changes');
  await expect(view.getByTestId(ids.tabDirty)).toHaveCount(2);
  await view.getByTestId(ids.tabGeneral).click();
  await expect(view.getByTestId(ids.generalName)).toHaveValue('Shop');

  await view.getByTestId(ids.discard).click();
  await expect(view.getByTestId(ids.generalName)).toHaveValue('Storefront');
  await expect(view.getByTestId(ids.unsaved)).toHaveCount(0);
});

test('Draft_WhenSaved_ShouldSendEveryTabInOneRequest', async ({ mount, page }) => {
  const puts = await stub(page);
  const view = await mount(<SettingsDraftHarness role="admin" />, {
    hooksConfig: { route: '/p/SHIP/settings/members' },
  });
  await view.getByTestId(ids.memberRole).nth(1).selectOption('admin');
  await view.getByTestId(ids.tabSprints).click();
  await view.getByTestId(ids.velocityOverride).fill('21.5');
  await view.getByTestId(ids.save).click();

  await expect(view.getByTestId(ids.unsaved)).toHaveCount(0);
  expect(puts).toHaveLength(1);
  expect(puts[0].members).toEqual([
    { email: 'joel@shiplean.local', role: 'admin' },
    { email: 'priya@shiplean.local', role: 'admin' },
  ]);
  expect(puts[0].sprints).toEqual({ velocityWindow: 3, velocityOverride: 21.5 });
});

test('Draft_WhenAgentFieldsEdited_ShouldSendThemWithGeneral', async ({ mount, page }) => {
  const puts = await stub(page);
  const view = await mount(<SettingsDraftHarness role="admin" />, {
    hooksConfig: { route: '/p/SHIP/settings/general' },
  });
  await expect(view.getByTestId(ids.generalRepoBaseBranch)).toHaveAttribute('placeholder', 'main');
  await view.getByTestId(ids.generalAgentConnection).selectOption('c-1');
  await view.getByTestId(ids.generalRepoUrl).fill(' https://github.com/ShipLean/ShipLean.git ');
  await view.getByTestId(ids.save).click();

  await expect(view.getByTestId(ids.unsaved)).toHaveCount(0);
  expect(puts[0].general).toEqual({
    name: 'Storefront',
    description: 'Shop',
    workingDays: [1, 2, 3, 4, 5],
    agentConnectionId: 'c-1',
    repoUrl: 'https://github.com/ShipLean/ShipLean.git',
    repoBaseBranch: null,
  });
});

test('Draft_WhenNoWorkingDay_ShouldJumpToTheFieldWithoutSaving', async ({ mount, page }) => {
  const puts = await stub(page);
  const view = await mount(<SettingsDraftHarness role="admin" />, {
    hooksConfig: { route: '/p/SHIP/settings/general' },
  });
  for (const day of [1, 2, 3, 4, 5]) {
    await view.getByTestId(`settings-working-day-${day}`).click();
  }
  await view.getByTestId(ids.tabMembers).click();
  await view.getByTestId(ids.save).click();

  await expect(view.getByTestId(ids.fieldError)).toContainText('at least one working day');
  await expect(view.getByTestId(ids.workingDays)).toBeFocused();
  expect(puts).toHaveLength(0);
});

test('Draft_WhenServerRefusesLastAdminChange_ShouldShowItsMessage', async ({ mount, page }) => {
  await stub(page, () => ({
    status: 400,
    json: { error: 'A project needs at least one admin.', code: 'validation' },
  }));
  const view = await mount(<SettingsDraftHarness role="admin" />, {
    hooksConfig: { route: '/p/SHIP/settings/members' },
  });
  await view.getByTestId(ids.memberRole).first().selectOption('member');
  await view.getByTestId(ids.save).click();

  await expect(view.getByTestId(ids.saveError)).toContainText('at least one admin');
  await expect(view.getByTestId(ids.unsaved)).toContainText('1 unsaved change');
});

test('Draft_WhenMemberEmailPartial_ShouldShowServerNotFound', async ({ mount, page }) => {
  await stub(page);
  await page.route('**/api/users/by-email**', (route) =>
    route.fulfill({
      status: 404,
      json: { error: 'No user has that email address.', code: 'not_found' },
    }),
  );
  const view = await mount(<SettingsDraftHarness role="admin" />, {
    hooksConfig: { route: '/p/SHIP/settings/members' },
  });
  await view.getByTestId(ids.memberAddEmail).fill('lee@shiplean');
  await view.getByTestId(ids.memberAdd).click();

  await expect(view.getByTestId(ids.memberAddError)).toContainText('No user has that email');
  await expect(view.getByTestId(ids.memberRow)).toHaveCount(2);
});

test('Draft_WhenMemberAdded_ShouldAppearAndRemovedMemberCanBeUndone', async ({ mount, page }) => {
  const puts = await stub(page);
  await page.route('**/api/users/by-email**', (route) =>
    route.fulfill({
      json: {
        id: 'u-lee',
        name: 'Lee Park',
        initials: 'LP',
        avatarColour: '#112233',
        photoUrl: null,
      },
    }),
  );
  const view = await mount(<SettingsDraftHarness role="admin" />, {
    hooksConfig: { route: '/p/SHIP/settings/members' },
  });
  await view.getByTestId(ids.memberAddEmail).fill('lee@shiplean.local');
  await view.getByTestId(ids.memberAdd).click();
  await expect(view.getByTestId(ids.memberRow)).toHaveCount(3);
  await view.getByTestId(ids.memberRemove).nth(1).click();
  await expect(view.getByTestId(ids.memberUndo)).toHaveCount(1);
  await view.getByTestId(ids.memberUndo).click();
  await view.getByTestId(ids.memberRemove).nth(1).click();
  await view.getByTestId(ids.save).click();

  await expect(view.getByTestId(ids.unsaved)).toHaveCount(0);
  expect(puts[0].members.map((m) => m.email)).toEqual([
    'joel@shiplean.local',
    'lee@shiplean.local',
  ]);
});

test('Draft_WhenMember_ShouldBeReadOnlyWithBanner', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<SettingsDraftHarness role="member" />, {
    hooksConfig: { route: '/p/SHIP/settings/general' },
  });
  await expect(view.getByTestId(ids.readOnlyBanner)).toContainText('Ask Joel Dickson');
  await expect(view.getByTestId(ids.generalName)).toBeDisabled();
  await expect(view.getByTestId(ids.save)).toHaveCount(0);
  await view.getByTestId(ids.tabMembers).click();
  await expect(view.getByTestId(ids.memberRole).first()).toBeDisabled();
  await expect(view.getByTestId(ids.memberAddEmail)).toHaveCount(0);
});

test('Draft_WhenStatesTab_ShouldShowTheStatesNodeUntouched', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<SettingsDraftHarness role="admin" />, {
    hooksConfig: { route: '/p/SHIP/settings/states' },
  });
  await expect(view.getByTestId('harness-states')).toBeVisible();
});

test('Draft_WhenStateStagedThroughContext_ShouldSurviveTabSwitchAndGoInTheOnePut', async ({
  mount,
  page,
}) => {
  const puts = await stub(page);
  const view = await mount(<SettingsDraftHarness role="admin" fakeStates />, {
    hooksConfig: { route: '/p/SHIP/settings/states' },
  });
  await view.getByTestId('fake-states-add').click();
  await expect(view.getByTestId(ids.tabDirty)).toHaveCount(1);
  await view.getByTestId(ids.tabGeneral).click();
  await view.getByTestId(ids.generalName).fill('Shop');
  await view.getByTestId(ids.tabStates).click();
  await expect(view.getByTestId('fake-states-count')).toHaveText('1');
  await expect(view.getByTestId(ids.unsaved)).toContainText('2 unsaved changes');
  await view.getByTestId(ids.save).click();

  await expect(view.getByTestId(ids.unsaved)).toHaveCount(0);
  expect(puts).toHaveLength(1);
  expect(puts[0].states.add).toEqual([
    { id: 'new-state', type: 'story', name: 'Review', category: 'inProgress' },
  ]);
  expect(puts[0].general.name).toBe('Shop');
});

const lee = {
  id: 'u-lee',
  name: 'Lee Park',
  initials: 'LP',
  avatarColour: '#112233',
  photoUrl: null,
};

test('Draft_WhenNewMemberRemoved_ShouldDropTheRowWithNoChange', async ({ mount, page }) => {
  const puts = await stub(page);
  await page.route('**/api/users/by-email**', (route) => route.fulfill({ json: lee }));
  const view = await mount(<SettingsDraftHarness role="admin" />, {
    hooksConfig: { route: '/p/SHIP/settings/members' },
  });
  await view.getByTestId(ids.memberAddEmail).fill('lee@shiplean.local');
  await view.getByTestId(ids.memberAdd).click();
  await expect(view.getByTestId(ids.memberRow)).toHaveCount(3);
  await view.getByTestId(ids.memberRemove).nth(2).click();

  await expect(view.getByTestId(ids.memberRow)).toHaveCount(2);
  await expect(view.getByTestId(ids.unsaved)).toHaveCount(0);
  await expect(view.getByTestId(ids.save)).toBeDisabled();
  expect(puts).toHaveLength(0);
});

test('Draft_WhenRemovedMemberAddedAgainByEmail_ShouldUndoTheRemoval', async ({ mount, page }) => {
  const puts = await stub(page);
  await page.route('**/api/users/by-email**', (route) => route.fulfill({ json: priya }));
  const view = await mount(<SettingsDraftHarness role="admin" />, {
    hooksConfig: { route: '/p/SHIP/settings/members' },
  });
  await view.getByTestId(ids.memberRemove).nth(1).click();
  await expect(view.getByTestId(ids.unsaved)).toContainText('1 unsaved change');
  await view.getByTestId(ids.memberAddEmail).fill('PRIYA@shiplean.local');
  await view.getByTestId(ids.memberAdd).click();

  await expect(view.getByTestId(ids.memberRow)).toHaveCount(2);
  await expect(view.getByTestId(ids.memberUndo)).toHaveCount(0);
  await expect(view.getByTestId(ids.unsaved)).toHaveCount(0);
  expect(puts).toHaveLength(0);
});

test('Draft_WhenCtrlS_ShouldSaveOnlyWithChangesAndOnlyOnce', async ({ mount, page }) => {
  const puts = await stub(page);
  const view = await mount(<SettingsDraftHarness role="admin" />, {
    hooksConfig: { route: '/p/SHIP/settings/general' },
  });
  await view.getByTestId(ids.generalName).press('Control+s');
  expect(puts).toHaveLength(0);

  await view.getByTestId(ids.generalName).fill('Shop');
  await view.getByTestId(ids.generalName).press('Control+s');
  await view.getByTestId(ids.generalName).press('Control+s');
  await expect(view.getByTestId(ids.unsaved)).toHaveCount(0);
  expect(puts).toHaveLength(1);
});

test('Draft_WhenUnsavedChangesClicked_ShouldListChangesAndJumpToEach', async ({ mount, page }) => {
  await stub(page);
  const view = await mount(<SettingsDraftHarness role="admin" />, {
    hooksConfig: { route: '/p/SHIP/settings/general' },
  });
  await view.getByTestId(ids.generalName).fill('Shop');
  await view.getByTestId(ids.tabSprints).click();
  await view.getByTestId(ids.velocityWindow).fill('4');
  await view.getByTestId(ids.unsaved).click();
  await expect(view.getByTestId(ids.changeItem)).toHaveText(['Name', 'Velocity window']);
  await view.getByTestId(ids.changeItem).first().click();

  await expect(view.getByTestId(ids.generalName)).toBeVisible();
});

test('Draft_WhenSaveFindsAnError_ShouldMarkItsTabAndTheChangedMemberRow', async ({
  mount,
  page,
}) => {
  await stub(page);
  const view = await mount(<SettingsDraftHarness role="admin" />, {
    hooksConfig: { route: '/p/SHIP/settings/members' },
  });
  await view.getByTestId(ids.memberRole).nth(1).selectOption('admin');
  await expect(view.getByTestId(ids.memberRow).nth(1)).toHaveAttribute('data-changed', 'true');
  await expect(view.getByTestId(ids.memberRow).first()).toHaveAttribute('data-changed', 'false');
  await view.getByTestId(ids.tabSprints).click();
  await view.getByTestId(ids.velocityWindow).fill('0');
  await view.getByTestId(ids.save).click();

  await expect(view.getByTestId(ids.tabError)).toHaveCount(1);
});
