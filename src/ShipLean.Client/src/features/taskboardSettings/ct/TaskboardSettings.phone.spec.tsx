import { expect, test } from '@playwright/experimental-ct-react';
import type { CardFieldSettingJson } from '@shiplean/core/api/persisted';
import { PROJECT_ID } from '../../boardSettings/ct/boardSettingsFixtures';
import { criteriaTestIds } from '../../criteria/testIds';
import { styleRule } from '../../boardSettings/ct/stubBoard';
import { settingsDialogTestIds as dialogIds } from '../../settingsDialog/testIds';
import { touchTestIds } from '../../../touch/testIds';
import { TaskboardSettingsDialog } from '../TaskboardSettingsDialog';
import { taskboardSettingsTestIds as ids } from '../testIds';
import { stubSettings, taskboardBoard } from './stubSettings';

// Taskboard settings on the CT phone project (390×844): full screen, section chips, the K5 SaveBar.

const task: CardFieldSettingJson = {
  type: 'task',
  showKey: true,
  showAssignee: true,
  showTags: true,
  showPoints: false,
  showEmptyFields: false,
  extraFields: ['priority'],
};
const blocked = styleRule('blocked', 'Blocked', {
  field: 'tags',
  op: 'contains',
  value: 'Blocked',
});

const dialog = (section: string, onClose: () => void = () => undefined) => (
  <TaskboardSettingsDialog
    projectId={PROJECT_ID}
    section={section}
    onSectionChange={() => undefined}
    onClose={onClose}
  />
);

test('TaskboardSettings_WhenOpenedOnPhone_ShouldFillScreenWithSectionChips', async ({
  mount,
  page,
}) => {
  await stubSettings(page, taskboardBoard({ cardFields: [task] }));
  await mount(dialog('styles'));
  const panel = page.getByRole('dialog');
  await expect(page.getByTestId(dialogIds.nav)).toBeVisible();
  await expect(page.getByTestId(ids.fieldsPane)).toHaveCount(0);

  const box = await panel.boundingBox();
  expect(box).toMatchObject({ x: 0, y: 0, width: 390, height: 844 });
  const close = await page.getByTestId(touchTestIds.dialogClose).boundingBox();
  expect(close?.x).toBeLessThan(40);

  const nav = page.getByTestId(dialogIds.nav);
  const chips = ['fields', 'styles', 'tags'].map((id) =>
    page.getByTestId(`${dialogIds.navItem}-${id}`),
  );
  const boxes = await Promise.all(chips.map((chip) => chip.boundingBox()));
  const ys = new Set(boxes.map((b) => b?.y));
  expect(ys.size, 'the chips are one row').toBe(1);
  for (const b of boxes) expect(b?.height).toBeGreaterThanOrEqual(32);
  await expect(chips[1]).toHaveAttribute('aria-current', 'page');
  await expect(page.getByTestId(`${dialogIds.navItem}-columns`)).toHaveCount(0);
  await expect(nav).toHaveCSS('overflow-x', 'auto');
});

test('TaskboardSettings_WhenDiscardTapped_ShouldRevertChanges', async ({ mount, page }) => {
  const puts = await stubSettings(page, taskboardBoard({ cardFields: [task] }));
  // A save that fails leaves the change unsaved: that is when the SaveBar shows in an apply-at-once dialog.
  await page.route('**/api/boards/*/settings', (route) =>
    route.request().method() === 'PUT'
      ? route.fulfill({ status: 500, json: { error: 'Boom', code: 'internal_error' } })
      : route.fallback(),
  );
  let closed = 0;
  await mount(dialog('fields', () => (closed += 1)));
  await expect(page.getByTestId(touchTestIds.saveBar)).toHaveCount(0);

  await page.getByTestId(ids.fieldsShowKey).setChecked(false);
  await expect(page.getByTestId(dialogIds.footerError)).toHaveText('Boom');
  const bar = page.getByTestId(touchTestIds.saveBar);
  await expect(bar).toBeVisible();
  await expect(bar.getByTestId(dialogIds.footerDirty)).toHaveText('Unsaved: Fields');

  await bar.getByTestId(touchTestIds.saveBarDiscard).tap();
  expect(closed).toBe(1);
  expect(puts).toHaveLength(0);
});

test('TaskboardSettings_WhenSaveFailedOnPhone_ShouldOfferSaveAndDiscardInTheSaveBar', async ({
  mount,
  page,
}) => {
  const puts = await stubSettings(page, taskboardBoard({ cardFields: [task] }));
  let attempts = 0;
  await page.route('**/api/boards/*/settings', (route) => {
    if (route.request().method() !== 'PUT') return route.fallback();
    attempts += 1;
    return attempts === 1
      ? route.fulfill({ status: 500, json: { error: 'Boom', code: 'internal_error' } })
      : route.fallback();
  });
  await mount(dialog('fields'));
  await page.getByTestId(ids.fieldsShowKey).setChecked(false);
  const bar = page.getByTestId(touchTestIds.saveBar);
  await expect(bar).toBeVisible();
  await expect(bar.getByTestId(touchTestIds.saveBarDiscard)).toBeVisible();

  await bar.getByTestId(touchTestIds.saveBarSave).tap();

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0]?.settings.cardFields[0]?.showKey).toBe(false);
  await expect(bar).toHaveCount(0);
  await expect(page.getByTestId(dialogIds.footerError)).toHaveCount(0);
});

test('TagColours_WhenOnPhone_ShouldShow32pxSwatches', async ({ mount, page }) => {
  await stubSettings(page, taskboardBoard({}), ['Blocked']);
  await mount(dialog('tags'));
  const swatches = page.getByTestId(ids.tagSwatch);
  await expect(swatches).toHaveCount(7);
  for (const swatch of await swatches.all()) {
    const box = await swatch.boundingBox();
    expect(box?.width).toBeGreaterThanOrEqual(32);
    expect(box?.height).toBeGreaterThanOrEqual(32);
  }
});

test('StyleRule_WhenOnPhone_ShouldStackItsClauses', async ({ mount, page }) => {
  await stubSettings(page, taskboardBoard({ styleRules: [blocked] }), ['Blocked']);
  await mount(dialog('styles'));
  const row = page.getByTestId(ids.styleRow);
  await expect(row).toBeVisible();

  const name = await row.getByTestId(ids.styleName).boundingBox();
  const colour = await row.getByTestId(ids.styleColour).boundingBox();
  const bold = await row.getByTestId(ids.styleBold).boundingBox();
  const criteria = await row.getByTestId(ids.styleCriteria).boundingBox();
  if (!name || !colour || !bold || !criteria) throw new Error('the rule should render');
  expect(colour.y, 'name and colour share a line').toBe(name.y);
  expect(bold.y, 'the format line sits below the name').toBeGreaterThan(name.y + name.height);
  expect(criteria.y, 'the clauses sit below the format line').toBeGreaterThan(bold.y + bold.height);
  for (const b of [name, colour, bold]) expect(b.height).toBeGreaterThanOrEqual(32);

  const clause = row.getByTestId(criteriaTestIds.clause).first();
  const field = await clause.getByTestId(criteriaTestIds.field).boundingBox();
  const op = await clause.getByTestId(criteriaTestIds.op).boundingBox();
  const value = await clause.getByTestId(criteriaTestIds.value).boundingBox();
  const remove = await clause.getByTestId(criteriaTestIds.remove).boundingBox();
  if (!field || !op || !value || !remove) throw new Error('the clause should render');
  expect(op.y, 'field and operator share a line').toBe(field.y);
  expect(value.y, 'the value is below them').toBeGreaterThan(field.y + field.height);
  expect(remove.y, 'the remove button sits with the value').toBe(value.y);
  expect(remove.height).toBeGreaterThanOrEqual(32);
  const doc = await page.evaluate(() => [
    document.documentElement.scrollWidth,
    document.documentElement.clientWidth,
  ]);
  expect(doc[0]).toBeLessThanOrEqual(doc[1]);
});

for (const theme of ['light', 'dark'] as const) {
  test(`TaskboardSettings_WhenFieldsOpened_ShouldMatchItsScreenshot_taskboard-settings-fields-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubSettings(page, taskboardBoard({ cardFields: [task, { ...task, type: 'story' }] }));
    await mount(dialog('fields'));
    await expect(page.getByTestId(ids.fieldsPane)).toBeVisible();
    await expect(page).toHaveScreenshot(`taskboard-settings-fields-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`TaskboardSettings_WhenTagsOpened_ShouldMatchItsScreenshot_taskboard-settings-tags-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubSettings(
      page,
      taskboardBoard({ tagColours: [{ tag: 'Blocked', colour: 'danger' }] }),
      ['Blocked', 'Spike'],
    );
    await mount(dialog('tags'));
    await expect(page.getByTestId(ids.tagRow)).toHaveCount(2);
    await expect(page).toHaveScreenshot(`taskboard-settings-tags-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
