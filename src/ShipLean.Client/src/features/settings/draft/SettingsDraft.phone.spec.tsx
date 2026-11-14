import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectSettingsDto } from '@shiplean/core/api/projects';
import { WORK_ITEM_TYPE_IDS } from '@shiplean/core/api/refs';
import type { Page } from '../../../pages/screenshots/shoot';
import { touchTestIds } from '../../../touch/testIds';
import { settingsTestIds as ids } from '../testIds';
import { SettingsDraftHarness } from './ct/SettingsDraftHarness';
import { PROJECT_ID } from './ct/projectDto';

// Project settings on a phone (SHIP-732, L732a): the tab row, the one SaveBar, General, Members and Sprints, on the CT
// `phone` project (390×844). Regenerate with scripts/update-page-shots.sh src/features/settings/draft/SettingsDraft.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const route = (tab: string) => ({ hooksConfig: { route: `/p/SHIP/settings/${tab}` } });

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
    await page.getByTestId(ids.tabSprints).tap();
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

test('SettingsPhone_WhenOpened_ShouldScrollTheTabsInTheirOwnBox', async ({ mount, page }) => {
  await stub(page);
  await mount(<SettingsDraftHarness role="admin" />, route('demos'));
  const row = page.getByTestId(ids.tabRow);
  const sizes = await row.evaluate((el) => ({ scroll: el.scrollWidth, client: el.clientWidth }));
  expect(sizes.scroll).toBeGreaterThan(sizes.client);
  // The active tab (the last one) is scrolled into view.
  const active = await page.getByTestId(ids.tabDemos).boundingBox();
  expect((active?.x ?? 0) + (active?.width ?? 0)).toBeLessThanOrEqual(390);
  const page_ = await page.evaluate(() => ({
    scroll: document.documentElement.scrollWidth,
    client: document.documentElement.clientWidth,
  }));
  expect(page_.scroll).toBeLessThanOrEqual(page_.client);
  await expect(page.getByTestId(touchTestIds.saveBar)).toHaveCount(0);
});

test('SettingsPhone_WhenChangesTapped_ShouldJumpToTheTab', async ({ mount, page }) => {
  await stub(page);
  await mount(<SettingsDraftHarness role="admin" />, route('general'));
  await page.getByTestId(ids.generalName).fill('Shop');
  await page.getByTestId(ids.tabMembers).tap();
  await page.getByTestId(ids.memberRemove).last().tap();
  await page.getByTestId(ids.tabSprints).tap();
  await page.getByTestId(ids.unsaved).tap();
  const items = page.getByTestId(touchTestIds.sheet).getByTestId(ids.changeItem);
  await expect(items).toHaveCount(2);
  await items.filter({ hasText: /Remove priya/ }).tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  await expect(page.getByTestId(ids.tabMembers)).toHaveAttribute('aria-current', 'page');
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

test('SettingsPhone_WhenDraftIsDirtyOnDemos_ShouldShowNoSaveBarButKeepTheTabDot', async ({
  mount,
  page,
}) => {
  await stub(page);
  await mount(<SettingsDraftHarness role="admin" />, route('general'));
  await page.getByTestId(ids.generalName).fill('Shop');
  await expect(page.getByTestId(touchTestIds.saveBar)).toBeVisible();

  await page.getByTestId(ids.tabDemos).tap();

  await expect(page.getByTestId(ids.tabDemos)).toHaveAttribute('aria-current', 'page');
  await expect(page.getByTestId(touchTestIds.saveBar)).toHaveCount(0);
  await expect(page.getByTestId(ids.tabDirty)).toHaveCount(1);
});

test('SettingsPhone_WhenReadOnly_ShouldDisableEveryEditControl', async ({ mount, page }) => {
  await stub(page);
  await mount(<SettingsDraftHarness role="member" />, route('general'));
  await expect(page.getByTestId(ids.generalName)).toBeDisabled();
  await expect(page.getByTestId(ids.workingDay1)).toBeDisabled();
  await page.getByTestId(ids.tabMembers).tap();
  await expect(page.getByTestId(ids.memberRole).first()).toBeDisabled();
  await expect(page.getByTestId(ids.memberRemove)).toHaveCount(0);
  await expect(page.getByTestId(ids.memberAddEmail)).toHaveCount(0);
  await expect(page.getByTestId(touchTestIds.saveBar)).toHaveCount(0);
});
