import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectSettingsDto, StateDto } from '@shiplean/core/api/projects';
import { PROJECT_ID } from '../draft/ct/projectDto';
import { StatesHarness } from '../ct/StatesHarness';
import { settingsTestIds as ids } from '../testIds';

// The States tab on a phone (SHIP-732, L732b, P5): the type select, two-line rows, the stacked delete form and the add
// form, as element shots of the States panel. Regenerate with
// scripts/update-page-shots.sh src/features/settings/states/SettingsStates.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;

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

const other = (type: 'epic' | 'feature' | 'bug' | 'task' | 'action' | 'bau'): StateDto[] => [
  { id: `${type}-new`, type, name: 'New', category: 'proposed', order: 1, itemCount: 0 },
  { id: `${type}-done`, type, name: 'Done', category: 'completed', order: 2, itemCount: 0 },
];

const settings = (): ProjectSettingsDto => ({
  general: {
    key: 'SHIP',
    name: 'Storefront',
    description: '',
    workingDays: [1, 2, 3, 4, 5],
    agentConnectionId: null,
    repoUrl: null,
    repoBaseBranch: null,
    holidayCountry: null,
    agentConnections: [],
  },
  members: [{ user: joel, email: 'joel@shiplean.local', role: 'admin', status: 'active' }],
  sprints: { velocityWindow: 3, velocityOverride: null },
  states: [
    st('s-new', 'New', 'proposed', 1, 0),
    st('s-active', 'Active', 'inProgress', 2, 3),
    st('s-closed', 'Closed', 'completed', 3, 4),
    ...(['epic', 'feature', 'bug', 'task', 'action', 'bau'] as const).flatMap(other),
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
        name: 'Ready for QA sign-off and release',
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
    handler: (route: { fulfill(response: { json: unknown }): Promise<void> }) => Promise<void>,
  ): Promise<unknown>;
}

const stub = async (page: Routable) => {
  await page.route(`**/api/projects/${PROJECT_ID}/settings`, (route) =>
    route.fulfill({ json: settings() }),
  );
  await page.route(`**/api/projects/${PROJECT_ID}/boards`, (route) =>
    route.fulfill({ json: [{ id: 'b-stories', level: 'stories', name: 'Stories' }] }),
  );
  await page.route('**/api/boards/b-stories/settings', (route) =>
    route.fulfill({ json: storiesBoard }),
  );
};

const phoneOnly = (page: { viewportSize(): { width: number } | null }) =>
  expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);

test('SettingsStatesPhone_WhenColumnIsLong_ShouldShowItWholeOnItsOwnLine', async ({
  mount,
  page,
}) => {
  phoneOnly(page);
  await stub(page);
  const view = await mount(<StatesHarness role="admin" />, {
    hooksConfig: { route: '/p/SHIP/settings/states' },
  });
  const rows = view.getByTestId(ids.statesPanel).getByTestId(ids.stateRow);
  await expect(rows).toHaveCount(3);
  const long = rows.nth(1).getByTestId(ids.stateColumn);
  const unmapped = rows.nth(2).getByTestId(ids.stateColumn);
  await expect(long).toHaveText('Ready for QA sign-off and release');
  await expect(unmapped).toHaveText('Not mapped');
  for (const cell of [long, unmapped]) {
    const fits = await cell.evaluate((e) => ({
      clipped: e.scrollWidth > e.clientWidth,
      label: getComputedStyle(e, '::before').content,
      ellipsis: getComputedStyle(e).textOverflow === 'ellipsis',
      wide: e.getBoundingClientRect().width,
    }));
    expect(fits.clipped).toBe(false);
    expect(fits.ellipsis).toBe(false);
    expect(fits.label).toContain('Board:');
    expect(fits.wide).toBeGreaterThan(300);
  }
});

for (const theme of THEMES) {
  const open = async (
    mount: Parameters<Parameters<typeof test>[2]>[0]['mount'],
    page: Parameters<Parameters<typeof test>[2]>[0]['page'],
  ) => {
    phoneOnly(page);
    // Taller than a phone, so the whole panel is in view for the element shot (the frame scrolls inside itself).
    await page.setViewportSize({ width: 390, height: 1600 });
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stub(page);
    const view = await mount(<StatesHarness role="admin" />, {
      hooksConfig: { route: '/p/SHIP/settings/states' },
    });
    const panel = view.getByTestId(ids.statesPanel);
    await expect(panel.getByTestId(ids.stateRow)).toHaveCount(3);
    return { view, panel };
  };

  test(`SettingsStatesPhone_WhenOpened_ShouldMatchItsScreenshot_settings-states-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    const { panel } = await open(mount, page);

    await expect(panel.getByTestId(ids.stateTypeSelect)).toBeVisible();
    await expect(panel).toHaveScreenshot(`settings-states-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`SettingsStatesPhone_WhenDeleteAsked_ShouldMatchItsScreenshot_settings-states-delete-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    const { panel } = await open(mount, page);

    await panel.getByTestId(ids.stateDelete).nth(1).click();

    await expect(panel.getByTestId(ids.stateDeleteForm)).toBeVisible();
    await expect(panel).toHaveScreenshot(`settings-states-delete-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`SettingsStatesPhone_WhenNameTyped_ShouldMatchItsScreenshot_settings-states-add-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    const { panel } = await open(mount, page);

    await panel.getByTestId(ids.stateAddName).fill('In review');

    await expect(panel.getByTestId(ids.stateAdd)).toBeEnabled();
    await expect(panel.getByTestId(ids.stateAdd)).toBeVisible();
    await expect(panel).toHaveScreenshot(`settings-states-add-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
