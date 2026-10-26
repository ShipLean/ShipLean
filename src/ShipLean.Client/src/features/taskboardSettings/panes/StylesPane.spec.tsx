import { expect, test } from '@playwright/experimental-ct-react';
import { TaskboardSettingsSlot } from '../../../slots/TaskboardSettingsSlot';
import { componentTestIds } from '../../../components/testIds';
import { TaskboardSlot } from '../../../slots/TaskboardSlot';
import { PROJECT_ID } from '../../boardSettings/ct/boardSettingsFixtures';
import { styleRule } from '../../boardSettings/ct/stubBoard';
import { settingsDialogTestIds as dialogIds } from '../../settingsDialog/testIds';
import { PROJECT, SPRINT, SPRINTS, VIEW } from '../../taskboard/ct/fixtures';
import { stubTaskboard } from '../../taskboard/ct/stubApi';
import { taskboardTestIds } from '../../taskboard/testIds';
import { stubSettings, taskboardBoard } from '../ct/stubSettings';
import { TaskboardSettingsDialog } from '../TaskboardSettingsDialog';
import { taskboardSettingsTestIds as ids } from '../testIds';

const blocked = styleRule('blocked', 'Blocked', {
  field: 'tags',
  op: 'contains',
  value: 'Blocked',
});
const urgent = styleRule('urgent', 'Urgent', { field: 'priority', op: '=', value: 1 });

const dialog = (
  <TaskboardSettingsDialog
    projectId={PROJECT_ID}
    section="styles"
    onSectionChange={() => undefined}
    onClose={() => undefined}
  />
);

test('TaskboardSettings_WhenRuleAdded_ShouldPutAtOnce', async ({ mount, page }) => {
  const puts = await stubSettings(page, taskboardBoard({ styleRules: [] }));
  await mount(dialog);
  await expect(page.getByTestId(dialogIds.save)).toHaveCount(0);
  await expect(page.getByTestId(ids.stylesEmpty)).toBeVisible();

  await page.getByTestId(ids.stylesAdd).click();

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].basedOn.styleRules).toEqual([]);
  expect(puts[0].settings.styleRules).toHaveLength(1);
  await expect(page.getByTestId(ids.styleRow)).toHaveCount(1);
  await expect(page.getByTestId(ids.stylesEmpty)).toHaveCount(0);
});

test('TaskboardSettings_WhenRuleMovedUp_ShouldPutTheNewOrder', async ({ mount, page }) => {
  const puts = await stubSettings(page, taskboardBoard({ styleRules: [blocked, urgent] }));
  await mount(dialog);

  await page.getByTestId(ids.styleGrip).nth(1).focus();
  await page.keyboard.press('Alt+ArrowUp');

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.styleRules.map((r) => r.id)).toEqual(['urgent', 'blocked']);
  expect(puts[0].basedOn.styleRules.map((r) => r.id)).toEqual(['blocked', 'urgent']);
  await expect(page.getByTestId(ids.styleName).first()).toHaveValue('Urgent');
});

test('TaskboardSettings_WhenRuleDeleted_ShouldPutWithoutIt', async ({ mount, page }) => {
  const puts = await stubSettings(page, taskboardBoard({ styleRules: [blocked, urgent] }));
  await mount(dialog);

  await page.getByTestId(ids.styleDelete).first().click();

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.styleRules.map((r) => r.id)).toEqual(['urgent']);
  await expect(page.getByTestId(ids.styleRow)).toHaveCount(1);
});

test('TaskboardSettings_WhenRuleDisabledAndEdited_ShouldPutEachChange', async ({ mount, page }) => {
  const puts = await stubSettings(page, taskboardBoard({ styleRules: [blocked] }));
  await mount(dialog);

  await page.getByTestId(ids.styleEnabled).uncheck();
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.styleRules[0].enabled).toBe(false);

  await page.getByTestId(ids.styleCriteria).getByTestId('criteria-value').fill('Spike');
  await page.keyboard.press('Enter');
  await expect.poll(() => puts.length).toBe(2);
  expect(JSON.stringify(puts[1].settings.styleRules[0].criteria)).toContain('Spike');
});

test('TaskboardSettingsSlot_WhenRuleAdded_ShouldRefreshTheOpenTaskboard', async ({
  mount,
  page,
}) => {
  const server = await stubTaskboard(page, VIEW);
  const puts = await stubSettings(page, taskboardBoard({ styleRules: [] }), [], PROJECT.id);
  await mount(
    <>
      <TaskboardSlot project={PROJECT} sprint={SPRINT} sprints={SPRINTS} />
      <TaskboardSettingsSlot
        project={PROJECT}
        sprint={SPRINT}
        sprints={SPRINTS}
        section="styles"
        onSectionChange={() => undefined}
        onClose={() => undefined}
      />
    </>,
    { hooksConfig: { route: '/p/SHIP/sprints/s24/taskboard' } },
  );
  await expect(page.getByTestId(taskboardTestIds.grid)).toBeVisible();
  await expect(page.getByTestId(ids.stylesPane)).toBeVisible();
  expect(server.reads).toBe(1);

  await page.getByTestId(ids.stylesAdd).click();

  await expect.poll(() => puts.length).toBe(1);
  await expect.poll(() => server.reads).toBe(2);
});

test('TaskboardSettings_WhenRuleNameTyped_ShouldPutOnceOnBlur', async ({ mount, page }) => {
  const puts = await stubSettings(page, taskboardBoard({ styleRules: [blocked] }));
  await mount(dialog);

  const name = page.getByTestId(ids.styleName);
  await name.fill('');
  await name.pressSequentially('Blocked work');
  await expect(name).toHaveValue('Blocked work');
  expect(puts).toHaveLength(0);
  await name.blur();

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.styleRules[0].name).toBe('Blocked work');
});

test('TaskboardSettings_WhenRuleNameTypedAndEnterPressed_ShouldPutOnce', async ({
  mount,
  page,
}) => {
  const puts = await stubSettings(page, taskboardBoard({ styleRules: [blocked] }));
  await mount(dialog);

  const name = page.getByTestId(ids.styleName);
  await name.fill('Stuck');
  await name.press('Enter');
  await name.blur();

  await expect.poll(() => puts.length).toBe(1);
  await expect(name).toHaveValue('Stuck');
  expect(puts[0].settings.styleRules[0].name).toBe('Stuck');
});

test('TaskboardSettings_WhenCriteriaTextTyped_ShouldPutOnceOnBlur', async ({ mount, page }) => {
  const puts = await stubSettings(page, taskboardBoard({ styleRules: [blocked] }));
  await mount(dialog);

  const value = page.getByTestId(ids.styleCriteria).getByTestId('criteria-value');
  await value.fill('');
  await value.pressSequentially('Spike');
  expect(puts).toHaveLength(0);
  await value.blur();

  await expect.poll(() => puts.length).toBe(1);
  expect(JSON.stringify(puts[0].settings.styleRules[0].criteria)).toContain('Spike');
});

test('TaskboardSettings_WhenCriteriaInvalid_ShouldHoldThemAndStillSaveOtherChanges', async ({
  mount,
  page,
}) => {
  const puts = await stubSettings(page, taskboardBoard({ styleRules: [blocked, urgent] }));
  await mount(dialog);

  const first = page.getByTestId(ids.styleRow).first();
  await first.getByTestId('criteria-field').selectOption('points');
  await expect(first.getByTestId('criteria-error').first()).toBeVisible();
  expect(puts).toHaveLength(0);

  await page.getByTestId(ids.styleBold).nth(1).click();

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.styleRules[0]).toEqual(blocked);
  expect(puts[0].settings.styleRules[1].bold).toBe(true);
  await expect(first.getByTestId('criteria-error').first()).toBeVisible();
});

test('TaskboardSettings_WhenLastClauseRemoved_ShouldSayInlineAndNotPut', async ({
  mount,
  page,
}) => {
  const puts = await stubSettings(page, taskboardBoard({ styleRules: [blocked] }));
  await mount(dialog);

  await page.getByTestId(ids.styleCriteria).getByTestId('criteria-remove').click();

  await expect(page.getByTestId('criteria-document-error')).toBeVisible();
  expect(puts).toHaveLength(0);
});

test('TaskboardSettings_WhenClosedWithAnInvalidRule_ShouldWarnFirst', async ({ mount, page }) => {
  await stubSettings(page, taskboardBoard({ styleRules: [blocked] }));
  let closed = 0;
  await mount(
    <TaskboardSettingsDialog
      projectId={PROJECT_ID}
      section="styles"
      onSectionChange={() => undefined}
      onClose={() => {
        closed += 1;
      }}
    />,
  );
  await page.getByTestId(ids.styleCriteria).getByTestId('criteria-remove').click();

  await page.getByTestId(componentTestIds.dialogClose).first().click();
  await expect(page.getByTestId(ids.unfinishedRule)).toBeVisible();
  expect(closed).toBe(0);
  await page.getByTestId(ids.keepEditing).click();
  await expect(page.getByTestId(ids.unfinishedRule)).toHaveCount(0);
  expect(closed).toBe(0);

  await page.getByTestId(componentTestIds.dialogClose).first().click();
  await page.getByTestId(ids.discardAndClose).click();
  expect(closed).toBe(1);
});

test('TaskboardSettings_WhenClosedWithValidRules_ShouldCloseAtOnce', async ({ mount, page }) => {
  await stubSettings(page, taskboardBoard({ styleRules: [blocked] }));
  let closed = 0;
  await mount(
    <TaskboardSettingsDialog
      projectId={PROJECT_ID}
      section="styles"
      onSectionChange={() => undefined}
      onClose={() => {
        closed += 1;
      }}
    />,
  );
  await expect(page.getByTestId(ids.styleRow)).toBeVisible();

  await page.getByTestId(componentTestIds.dialogClose).first().click();

  expect(closed).toBe(1);
});
