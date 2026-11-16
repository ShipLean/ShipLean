import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectSettingsDto, StateDto } from '@shiplean/core/api/projects';
import { PROJECT_ID } from '../draft/ct/projectDto';
import { StatesHarness } from '../ct/StatesHarness';
import { settingsTestIds as ids } from '../testIds';
import { touchTestIds } from '../../../touch/testIds';

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

interface Point {
  x: number;
  y: number;
}
type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

// Holds the ⠿ handle for the long press (the shared hook's 450ms) and drags to `to`; lets go unless `hold`. Returns
// the sender, so a held touch can be cancelled the way the browser does.
async function touchDrag(page: Page, from: Point, to: Point, hold = false) {
  const cdp = await page.context().newCDPSession(page);
  const send = (type: 'touchStart' | 'touchMove' | 'touchEnd' | 'touchCancel', point?: Point) =>
    cdp.send('Input.dispatchTouchEvent', { type, touchPoints: point ? [point] : [] });
  await page.clock.install({ time: 0 });
  await page.clock.pauseAt(1000);
  await send('touchStart', from);
  await page.clock.runFor(450);
  const steps = Math.max(1, Math.ceil(Math.hypot(to.x - from.x, to.y - from.y) / 20));
  for (let i = 1; i <= steps; i++) {
    await send('touchMove', {
      x: from.x + ((to.x - from.x) * i) / steps,
      y: from.y + ((to.y - from.y) * i) / steps,
    });
  }
  if (!hold) await send('touchEnd');
  return send;
}

const centre = async (locator: {
  boundingBox(): Promise<(Point & { width: number; height: number }) | null>;
}) => {
  const box = await locator.boundingBox();
  if (!box) throw new Error('no box');
  return { x: Math.round(box.x + box.width / 2), y: Math.round(box.y + box.height / 2) };
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
  for (const i of [1, 2]) {
    // The label is its own element, shown only at touch, so the column cell's text stays the column name.
    await expect(rows.nth(i).getByTestId(ids.stateColumnLabel)).toBeVisible();
    await expect(rows.nth(i).getByTestId(ids.stateColumnLabel)).toHaveText('Board column ·');
  }
  for (const cell of [long, unmapped]) {
    const fits = await cell.evaluate((e) => {
      const td = e.parentElement;
      if (!td) throw new Error('no cell');
      return {
        clipped: td.scrollWidth > td.clientWidth,
        ellipsis: getComputedStyle(td).textOverflow === 'ellipsis',
        wide: td.getBoundingClientRect().width,
      };
    });
    expect(fits.clipped).toBe(false);
    expect(fits.ellipsis).toBe(false);
    expect(fits.wide).toBeGreaterThan(300);
  }
});

// ND3a-3/ND3a-4 (SHIP-1809 P4, P5): ⠿ handle · name · ⋯ on line 1, no ↑/↓ or Delete buttons, "Items n" on line 2.
test('SettingsStatesPhone_WhenOpened_ShouldPutHandleNameAndMoreOnTheFirstLine', async ({
  mount,
  page,
}) => {
  phoneOnly(page);
  await stub(page);
  const view = await mount(<StatesHarness role="admin" />, {
    hooksConfig: { route: '/p/SHIP/settings/states' },
  });
  const panel = view.getByTestId(ids.statesPanel);
  const rows = panel.getByTestId(ids.stateRow);
  await expect(rows).toHaveCount(3);
  // Nothing in the panel is wider than the panel: every row, and its ⋯, fits the 390px screen.
  const spill = await panel.evaluate((p) => ({
    panel: p.scrollWidth - p.clientWidth,
    page: document.documentElement.scrollWidth - document.documentElement.clientWidth,
    right: p.getBoundingClientRect().right,
    rows: Array.from(p.querySelectorAll('tr')).map((r) => r.getBoundingClientRect().right),
  }));
  expect(spill.panel).toBe(0);
  expect(spill.page).toBe(0);
  for (const right of spill.rows) expect(right).toBeLessThanOrEqual(spill.right);
  for (let i = 0; i < 3; i++) {
    const row = rows.nth(i);
    for (const id of [ids.stateMoveUp, ids.stateMoveDown, ids.stateDelete])
      await expect(row.getByTestId(id)).toBeHidden();
    const handle = await row.getByTestId(ids.stateHandle).boundingBox();
    const more = await row.getByTestId(touchTestIds.moreButton).boundingBox();
    const name = await row.getByTestId(ids.stateName).boundingBox();
    const category = await row.getByTestId(ids.stateCategory).boundingBox();
    if (!handle || !more || !name || !category) throw new Error('row part not drawn');
    expect(handle.width).toBeGreaterThanOrEqual(32);
    expect(more.width).toBeGreaterThanOrEqual(32);
    const mid = (b: { y: number; height: number }) => b.y + b.height / 2;
    expect(Math.abs(mid(handle) - mid(name))).toBeLessThanOrEqual(2);
    expect(Math.abs(mid(more) - mid(name))).toBeLessThanOrEqual(2);
    expect(handle.x).toBeLessThan(name.x);
    expect(more.x).toBeGreaterThan(name.x + name.width - 1);
    expect(category.y).toBeGreaterThan(name.y + name.height - 1);
    expect(category.x).toBeGreaterThanOrEqual(name.x - 16);
    expect(more.x + more.width).toBeLessThanOrEqual(spill.right);
  }
  const label = await rows
    .nth(1)
    .getByTestId(ids.stateItems)
    .evaluate((e) => getComputedStyle(e, '::before').content);
  expect(label).toContain('Items');
});

// ND3a-4 (P5): the ⋯ sheet holds Rename, Move up, Move down and Delete…, and its moves change the draft.
test('SettingsStatesPhone_WhenMoreTapped_ShouldOfferTheRowActionsAndMoveUp', async ({
  mount,
  page,
}) => {
  phoneOnly(page);
  await stub(page);
  const view = await mount(<StatesHarness role="admin" />, {
    hooksConfig: { route: '/p/SHIP/settings/states' },
  });
  const panel = view.getByTestId(ids.statesPanel);
  await panel.getByTestId(ids.stateRow).nth(1).getByTestId(touchTestIds.moreButton).tap();
  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet).toContainText('Active');
  await expect(sheet.getByTestId(touchTestIds.actionSheetRow)).toHaveText([
    'Rename',
    'Move up',
    'Move down',
    'Delete…',
  ]);
  await sheet.locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="up"]`).tap();
  await expect(sheet).toHaveCount(0);
  await expect(panel.getByTestId(ids.stateName).first()).toHaveValue('Active');
  await expect(panel.getByTestId(ids.stateName).nth(1)).toHaveValue('New');

  await panel.getByTestId(ids.stateRow).nth(0).getByTestId(touchTestIds.moreButton).tap();
  await page
    .getByTestId(touchTestIds.sheet)
    .locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="delete"]`)
    .tap();
  await expect(panel.getByTestId(ids.stateDeleteForm)).toBeVisible();

  // D3: a blocked Delete… is a disabled row with the reason as its hint.
  await panel.getByTestId(ids.stateRow).nth(2).getByTestId(touchTestIds.moreButton).tap();
  const blocked = page
    .getByTestId(touchTestIds.sheet)
    .locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="delete"]`);
  await expect(blocked).toBeDisabled();
  await expect(blocked.getByTestId(touchTestIds.actionSheetHint)).toHaveText(
    'This is the last Completed state. Add or recategorise another first.',
  );
});

// ND3a-4 (P5): ⋯ › Rename puts the cursor in the row's name, after the sheet has handed focus back to the ⋯.
test('SettingsStatesPhone_WhenRenameTapped_ShouldFocusTheRowsName', async ({ mount, page }) => {
  phoneOnly(page);
  await stub(page);
  const view = await mount(<StatesHarness role="admin" />, {
    hooksConfig: { route: '/p/SHIP/settings/states' },
  });
  const row = view.getByTestId(ids.statesPanel).getByTestId(ids.stateRow).nth(1);
  await row.getByTestId(touchTestIds.moreButton).tap();
  await page
    .getByTestId(touchTestIds.sheet)
    .locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="rename"]`)
    .tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  await expect(row.getByTestId(ids.stateName)).toBeFocused();
});

// ND3a-3 (P4): hold the ⠿ handle and drag: the row moves where it is dropped.
test('SettingsStatesPhone_WhenHandleDragged_ShouldMoveTheStateToTheDrop', async ({
  mount,
  page,
}) => {
  phoneOnly(page);
  await stub(page);
  const view = await mount(<StatesHarness role="admin" />, {
    hooksConfig: { route: '/p/SHIP/settings/states' },
  });
  const panel = view.getByTestId(ids.statesPanel);
  const rows = panel.getByTestId(ids.stateRow);
  await expect(rows).toHaveCount(3);
  const order = () =>
    panel
      .getByTestId(ids.stateName)
      .evaluateAll((all) => all.map((e) => (e as HTMLInputElement).value));
  expect(await order()).toEqual(['New', 'Active', 'Closed']);
  await touchDrag(
    page,
    await centre(rows.first().getByTestId(ids.stateHandle)),
    await centre(rows.last()),
  );
  await expect.poll(order).toEqual(['Active', 'Closed', 'New']);
});

// ND3a-3 (P4): a touch the browser cancels mid-drag leaves no drop line and no lifted row, and moves nothing.
test('SettingsStatesPhone_WhenDragCancelled_ShouldClearTheDropLine', async ({ mount, page }) => {
  phoneOnly(page);
  await stub(page);
  const view = await mount(<StatesHarness role="admin" />, {
    hooksConfig: { route: '/p/SHIP/settings/states' },
  });
  const panel = view.getByTestId(ids.statesPanel);
  const rows = panel.getByTestId(ids.stateRow);
  await expect(rows).toHaveCount(3);
  const shadow = () => rows.nth(1).evaluate((e) => getComputedStyle(e).boxShadow);
  expect(await shadow()).toBe('none');
  const send = await touchDrag(
    page,
    await centre(rows.first().getByTestId(ids.stateHandle)),
    await centre(rows.nth(1)),
    true,
  );
  // The drop line is the signal-coloured inset shadow on the row under the finger.
  await expect.poll(shadow).not.toBe('none');
  await send('touchCancel');
  await expect(rows.first()).not.toHaveAttribute('data-lifted');
  await expect.poll(shadow).toBe('none');
  expect(
    await panel
      .getByTestId(ids.stateName)
      .evaluateAll((all) => all.map((e) => (e as HTMLInputElement).value)),
  ).toEqual(['New', 'Active', 'Closed']);
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

    await panel.getByTestId(ids.stateRow).nth(1).getByTestId(touchTestIds.moreButton).tap();
    await page
      .getByTestId(touchTestIds.sheet)
      .locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="delete"]`)
      .tap();

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
  test(`SettingsStatesPhone_WhenMoreTapped_ShouldMatchItsScreenshot_settings-states-menu-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stub(page);
    const view = await mount(<StatesHarness role="admin" />, {
      hooksConfig: { route: '/p/SHIP/settings/states' },
    });
    const rows = view.getByTestId(ids.statesPanel).getByTestId(ids.stateRow);
    await expect(rows).toHaveCount(3);
    await rows.nth(1).getByTestId(touchTestIds.moreButton).tap();
    await expect(page.getByTestId(touchTestIds.actionSheetRow)).toHaveCount(4);
    await expect(page).toHaveScreenshot(`settings-states-menu-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });
  test(`SettingsStatesPhone_WhenDragging_ShouldMatchItsScreenshot_settings-states-drag-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    const { panel } = await open(mount, page);
    const rows = panel.getByTestId(ids.stateRow);
    await touchDrag(
      page,
      await centre(rows.first().getByTestId(ids.stateHandle)),
      await centre(rows.nth(1)),
      true,
    );
    await expect(rows.first()).toHaveAttribute('data-lifted', 'true');
    await expect(panel).toHaveScreenshot(`settings-states-drag-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
