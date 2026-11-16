import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectSettingsDto } from '@shiplean/core/api/projects';
import { WORK_ITEM_TYPE_IDS } from '@shiplean/core/api/refs';
import type { Page } from '../../../pages/screenshots/shoot';
import { componentTestIds } from '../../../components/testIds';
import { touchTestIds } from '../../../touch/testIds';
import { settingsTestIds as ids } from '../testIds';
import { SettingsDraftHarness } from './ct/SettingsDraftHarness';
import { PROJECT_ID } from './ct/projectDto';

// Project settings on a phone (SHIP-732, L732a): the tab row, the one SaveBar, General, Members and Sprints, on the CT
// `phone` project (390×844). Regenerate with scripts/update-page-shots.sh src/features/settings/draft/SettingsDraft.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const route = (tab: string) => ({ hooksConfig: { route: `/p/SHIP/settings/${tab}` } });

// ‹ back to the index (the top bar's link, hidden in the harness), then a pane's row.
const openPane = async (page: Page, pane: RegExp) => {
  await page.getByTestId('harness-back').dispatchEvent('click');
  await page.getByTestId(ids.settingsIndexRow).filter({ hasText: pane }).tap();
};

const person = (id: string, name: string, initials: string, avatarColour: string) => ({
  id,
  name,
  initials,
  avatarColour,
  photoUrl: null,
});
const joel = person('u-joel', 'Joel Dickson', 'JD', '#5b6b8c');
const priya = person('u-priya', 'Priya Shah', 'PS', '#2f7fd8');

const saved = (): ProjectSettingsDto => ({
  general: {
    key: 'SHIP',
    name: 'Storefront',
    description: 'The shop, its checkout and the mobile app.',
    workingDays: [1, 2, 3, 4, 5],
    agentConnectionId: null,
    repoUrl: null,
    repoBaseBranch: null,
    holidayCountry: null,
    agentConnections: [{ id: 'c-1', name: 'Home relay', archived: false }],
  },
  members: [
    { user: joel, email: 'joel@shiplean.local', role: 'admin', status: 'active' },
    { user: priya, email: 'priya.shah@storefront-example.com', role: 'member', status: 'active' },
  ],
  sprints: { velocityWindow: 3, velocityOverride: null, sprintNaming: 'number' },
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

const stub = async (page: Page) => {
  await page.route('**/api/holidays/countries', async (r) => {
    await r.fulfill({ json: [{ code: 'AU', name: 'Australia' }] });
  });
  await page.route(`**/api/projects/${PROJECT_ID}/settings`, async (r) => {
    await r.fulfill({ json: saved() });
  });
};

for (const theme of THEMES) {
  test(`SettingsPhone_WhenGeneralOpened_ShouldMatchItsScreenshot_settings-general-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stub(page);
    await mount(<SettingsDraftHarness role="admin" />, route('general'));
    await expect(page.getByTestId(ids.generalName)).toHaveValue('Storefront');
    await expect(page).toHaveScreenshot(`settings-general-phone.${theme}.png`, {
      animations: 'disabled',
      fullPage: true,
    });
  });

  test(`SettingsPhone_WhenEdited_ShouldMatchItsScreenshot_settings-general-dirty-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stub(page);
    await mount(<SettingsDraftHarness role="admin" />, route('general'));
    await page.getByTestId(ids.generalName).fill('Shop');
    await expect(page.getByTestId(touchTestIds.saveBar)).toBeVisible();
    await expect(page).toHaveScreenshot(`settings-general-dirty-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`SettingsPhone_WhenMembersOpened_ShouldMatchItsScreenshot_settings-members-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stub(page);
    await mount(<SettingsDraftHarness role="admin" />, route('members'));
    await expect(page.getByTestId(ids.memberRow)).toHaveCount(2);
    await page.getByTestId(ids.memberRemove).last().tap();
    await expect(page.getByTestId(ids.memberUndo)).toBeVisible();
    await expect(page).toHaveScreenshot(`settings-members-phone.${theme}.png`, {
      animations: 'disabled',
      fullPage: true,
    });
  });

  test(`SettingsPhone_WhenSprintsOpened_ShouldMatchItsScreenshot_settings-sprints-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stub(page);
    await mount(<SettingsDraftHarness role="admin" />, route('sprints'));
    await page.getByTestId(ids.velocityOverride).fill('21');
    await expect(page.getByTestId(ids.velocityClear)).toBeVisible();
    await expect(page).toHaveScreenshot(`settings-sprints-phone.${theme}.png`, {
      animations: 'disabled',
      fullPage: true,
    });
  });

  test(`SettingsPhone_WhenChangesOpened_ShouldMatchItsScreenshot_settings-changes-sheet_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stub(page);
    await mount(<SettingsDraftHarness role="admin" />, route('general'));
    await page.getByTestId(ids.generalName).fill('Shop');
    await openPane(page, /^Sprints & forecast/);
    await page.getByTestId(ids.velocityWindow).fill('4');
    await page.getByTestId(ids.unsaved).tap();
    const sheet = page.getByTestId(touchTestIds.sheet);
    await expect(sheet.getByTestId(ids.changeItem)).toHaveCount(2);
    await expect(sheet).toHaveScreenshot(`settings-changes-sheet.${theme}.png`, {
      animations: 'disabled',
    });
  });
}

test('SettingsPhone_WhenEdited_ShouldShowSixteenPixelFullWidthFieldsAndTheSaveBar', async ({
  mount,
  page,
}) => {
  await stub(page);
  await mount(<SettingsDraftHarness role="admin" />, route('general'));
  const name = page.getByTestId(ids.generalName);
  await expect(name).toHaveCSS('font-size', '16px');
  const field = await name.boundingBox();
  const panel = await page.getByLabel('Project', { exact: true }).boundingBox();
  if (!field || !panel) throw new Error('the name field and its panel should render');
  expect(field.width).toBeGreaterThan(panel.width - 40);
  await expect(page.getByTestId(ids.save)).toHaveCount(0);

  await name.fill('Shop');
  await expect(page.getByTestId(ids.unsaved)).toHaveText('1 unsaved change');
  await expect(page.getByTestId(touchTestIds.saveBarSave)).toBeVisible();
  await page.getByTestId(touchTestIds.saveBarDiscard).tap();
  await expect(name).toHaveValue('Storefront');
  await expect(page.getByTestId(touchTestIds.saveBar)).toHaveCount(0);
});

// SHIP-1809 P1, P2: no tab row or header card at touch; the tab-less address lists the panes, a pane is a sub-page.
test('SettingsPhone_WhenOpenedWithNoPane_ShouldListThePanesWithoutTabsOrHeader', async ({
  mount,
  page,
}) => {
  await stub(page);
  await mount(<SettingsDraftHarness role="admin" />, {
    hooksConfig: { route: '/p/SHIP/settings' },
  });
  const rows = page.getByTestId(ids.settingsIndexRow);
  await expect(rows).toHaveText([
    /^General/,
    /^States/,
    /^Members/,
    /^Sprints & forecast/,
    /^Integrations/,
    /^Demos/,
    /^Inspection/,
  ]);
  await expect(page.getByTestId(ids.tabRow)).toHaveCount(0);
  await expect(page.getByTestId('harness-back')).toHaveCount(0);
  await rows.filter({ hasText: /^Members/ }).tap();
  await expect(page.getByTestId(ids.membersPanel)).toBeVisible();
  await expect(page.getByTestId(ids.tabRow)).toHaveCount(0);
  await expect(page.getByTestId('harness-back')).toHaveAttribute('href', '/p/SHIP/settings');
  const page_ = await page.evaluate(() => ({
    scroll: document.documentElement.scrollWidth,
    client: document.documentElement.clientWidth,
  }));
  expect(page_.scroll).toBeLessThanOrEqual(page_.client);
  await expect(page.getByTestId(touchTestIds.saveBar)).toHaveCount(0);
});

// ND3a-9 (SHIP-1809 P3): the Save bar's message is a soft chip, ● count ▾, at least one touch target tall.
test('SettingsPhone_WhenEdited_ShouldShowTheCountAsAChipWithADotAndCaret', async ({
  mount,
  page,
}) => {
  await stub(page);
  await mount(<SettingsDraftHarness role="admin" />, route('general'));
  await page.getByTestId(ids.generalName).fill('Shop');
  const chip = page.getByTestId(ids.unsavedChip);
  await expect(chip).toHaveText('1 unsaved change▾');
  await expect(chip).toHaveAttribute('aria-haspopup', 'dialog');
  await expect(chip).not.toHaveCSS('background-color', 'rgba(0, 0, 0, 0)');
  expect((await chip.boundingBox())?.height).toBeGreaterThanOrEqual(32);
  await chip.tap();
  await expect(page.getByTestId(touchTestIds.sheet).getByTestId(ids.changeItem)).toHaveCount(1);
});

// ND3a-10 (SHIP-1809 P3): each change names its pane in a column of its own, then the change.
test('SettingsPhone_WhenChangesOpened_ShouldNameThePaneBesideEachChange', async ({
  mount,
  page,
}) => {
  await stub(page);
  await mount(<SettingsDraftHarness role="admin" />, route('general'));
  await page.getByTestId(ids.generalName).fill('Shop');
  await openPane(page, /^Sprints & forecast/);
  await page.getByTestId(ids.velocityWindow).fill('4');
  await page.getByTestId(ids.unsaved).tap();
  const sheet = page.getByTestId(touchTestIds.sheet);
  await expect(sheet.getByTestId(ids.changePane)).toHaveText(['General', 'Sprints & forecast']);
  const items = sheet.getByTestId(ids.changeItem);
  await expect(items).toHaveText(['GeneralName', 'Sprints & forecastVelocity window']);
  const panes = sheet.getByTestId(ids.changePane);
  const [short, long, row] = await Promise.all([
    panes.first().boundingBox(),
    panes.last().boundingBox(),
    items.first().boundingBox(),
  ]);
  if (!short || !long || !row) throw new Error('change row part not drawn');
  // The pane sits beside the change on one line, and both panes take one column's width, so the changes line up.
  expect(Math.abs(short.y + short.height / 2 - (row.y + row.height / 2))).toBeLessThanOrEqual(4);
  expect(Math.abs(short.width - long.width)).toBeLessThanOrEqual(1);
  expect(short.x + short.width).toBeLessThan(row.x + row.width / 2);
});

test('SettingsPhone_WhenChangesTapped_ShouldJumpToTheTab', async ({ mount, page }) => {
  await stub(page);
  await mount(<SettingsDraftHarness role="admin" />, route('general'));
  await page.getByTestId(ids.generalName).fill('Shop');
  await openPane(page, /^Members/);
  await page.getByTestId(ids.memberRemove).last().tap();
  await openPane(page, /^Sprints & forecast/);
  await page.getByTestId(ids.unsaved).tap();
  const items = page.getByTestId(touchTestIds.sheet).getByTestId(ids.changeItem);
  await expect(items).toHaveCount(2);
  await items.filter({ hasText: /Remove priya/ }).tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  await expect(page.getByTestId(ids.membersPanel)).toBeVisible();
});

test('SettingsPhone_WhenMemberRemoved_ShouldKeepRemoveVisibleAndUndoable', async ({
  mount,
  page,
}) => {
  await stub(page);
  await mount(<SettingsDraftHarness role="admin" />, route('members'));
  const remove = page.getByTestId(ids.memberRemove).last();
  const box = await remove.boundingBox();
  expect(box?.height).toBeGreaterThanOrEqual(32);
  await remove.tap();
  const undo = page.getByTestId(ids.memberUndo);
  expect((await undo.boundingBox())?.height).toBeGreaterThanOrEqual(32);
  await undo.tap();
  await expect(page.getByTestId(ids.memberRemove)).toHaveCount(2);
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth <= document.documentElement.clientWidth,
  );
  expect(overflow).toBe(true);
});

test('SettingsPhone_WhenReadOnly_ShouldShowNoSaveBar', async ({ mount, page }) => {
  await stub(page);
  await mount(<SettingsDraftHarness role="member" />, route('general'));
  await expect(page.getByTestId(ids.readOnlyBanner)).toBeVisible();
  await expect(page.getByTestId(touchTestIds.saveBar)).toHaveCount(0);
});

test('SettingsPhone_WhenDraftIsDirtyOnDemos_ShouldShowNoSaveBarButKeepTheIndexDot', async ({
  mount,
  page,
}) => {
  await stub(page);
  await mount(<SettingsDraftHarness role="admin" />, route('general'));
  await page.getByTestId(ids.generalName).fill('Shop');
  await expect(page.getByTestId(touchTestIds.saveBar)).toBeVisible();

  await page.getByTestId('harness-back').dispatchEvent('click');
  await expect(page.getByTestId(touchTestIds.saveBar)).toBeVisible();
  await expect(page.getByTestId(ids.tabDirty)).toHaveCount(1);
  await page
    .getByTestId(ids.settingsIndexRow)
    .filter({ hasText: /^Demos/ })
    .tap();

  await expect(page.getByTestId(touchTestIds.saveBar)).toHaveCount(0);
});

test('SettingsPhone_WhenReadOnly_ShouldDisableEveryEditControl', async ({ mount, page }) => {
  await stub(page);
  await mount(<SettingsDraftHarness role="member" />, route('general'));
  await expect(page.getByTestId(ids.generalName)).toBeDisabled();
  await expect(page.getByTestId(ids.workingDay1)).toBeDisabled();
  await openPane(page, /^Members/);
  await expect(page.getByTestId(ids.memberRole).first()).toBeDisabled();
  await expect(page.getByTestId(ids.memberRemove)).toHaveCount(0);
  await expect(page.getByTestId(ids.memberAddEmail)).toHaveCount(0);
  await expect(page.getByTestId(touchTestIds.saveBar)).toHaveCount(0);
});

// ND3a-7 (SHIP-1809 P6): a member row is two lines: name · role · ✕ on the first, the email under the name.
test('SettingsPhone_WhenMembersShown_ShouldPutNameRoleAndRemoveOnTheFirstLine', async ({
  mount,
  page,
}) => {
  await stub(page);
  await mount(<SettingsDraftHarness role="admin" />, route('members'));
  const rows = page.getByTestId(ids.memberRow);
  await expect(rows.first()).toBeVisible();
  const row = rows.last();
  const remove = row.getByTestId(ids.memberRemove);
  expect(await remove.innerText()).toBe('✕');
  const [name, whole, role, cross] = await Promise.all([
    row.getByTestId(componentTestIds.avatar).boundingBox(),
    row.boundingBox(),
    row.getByTestId(ids.memberRole).boundingBox(),
    remove.boundingBox(),
  ]);
  if (!name || !whole || !role || !cross) throw new Error('row part not drawn');
  const mid = (b: { y: number; height: number }) => b.y + b.height / 2;
  expect(Math.abs(mid(role) - mid(name))).toBeLessThanOrEqual(4);
  expect(Math.abs(mid(cross) - mid(name))).toBeLessThanOrEqual(4);
  expect(cross.width).toBeGreaterThanOrEqual(32);
  expect(cross.height).toBeGreaterThanOrEqual(32);
  expect(role.x).toBeGreaterThan(name.x);
  // Two lines: the role line plus the email line, not a third line for role and Remove.
  expect(whole.height).toBeLessThan(role.height + 40);
});
