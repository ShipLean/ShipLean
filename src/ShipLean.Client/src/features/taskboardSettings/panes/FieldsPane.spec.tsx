import { expect, test } from '@playwright/experimental-ct-react';
import type { BoardSettingsDto, BoardSettingsPut } from '../../../api/boards';
import type { CardFieldSettingJson } from '../../../api/persisted';
import { settingsDialogTestIds as dialogIds } from '../../settingsDialog/testIds';
import {
  boardSettingsDto,
  emptySettings,
  PROJECT_ID,
  TASKBOARD_ID,
} from '../../boardSettings/ct/boardSettingsFixtures';
import { TaskboardSettingsDialog } from '../TaskboardSettingsDialog';
import { taskboardSettingsTestIds as ids } from '../testIds';

const setting = (
  type: CardFieldSettingJson['type'],
  extraFields: string[],
): CardFieldSettingJson => ({
  type,
  showKey: true,
  showAssignee: true,
  showTags: true,
  showPoints: false,
  showEmptyFields: false,
  extraFields,
});

const TEN = [
  'priority',
  'sprint',
  'parent',
  'createdAt',
  'createdBy',
  'changedAt',
  'changedBy',
  'state',
  'severity',
  'description',
];

const board = (cardFields: CardFieldSettingJson[]): BoardSettingsDto =>
  boardSettingsDto({
    id: TASKBOARD_ID,
    level: 'taskboard',
    settings: { ...emptySettings(), cardFields },
  });

interface Routable {
  route(
    url: string,
    handler: (route: {
      request(): { method(): string; postDataJSON(): unknown };
      fulfill(response: { json: unknown }): Promise<void>;
    }) => Promise<void>,
  ): Promise<unknown>;
}

async function stub(page: Routable, dto: BoardSettingsDto): Promise<BoardSettingsPut[]> {
  const puts: BoardSettingsPut[] = [];
  await page.route(`**/api/projects/${PROJECT_ID}/boards`, (route) =>
    route.fulfill({ json: [{ id: TASKBOARD_ID, level: 'taskboard', name: 'Taskboard' }] }),
  );
  await page.route(`**/api/boards/${TASKBOARD_ID}/settings`, async (route) => {
    if (route.request().method() === 'GET') return route.fulfill({ json: dto });
    const body = route.request().postDataJSON() as BoardSettingsPut;
    puts.push(body);
    return route.fulfill({
      json: { board: { ...dto, settings: body.settings }, mergedSections: [] },
    });
  });
  return puts;
}

const dialog = (
  <TaskboardSettingsDialog
    projectId={PROJECT_ID}
    section="fields"
    onSectionChange={() => undefined}
    onClose={() => undefined}
  />
);

test('TaskboardSettings_WhenFieldToggled_ShouldPutAtOnce', async ({ mount, page }) => {
  const puts = await stub(page, board([setting('task', ['parent']), setting('story', ['state'])]));
  await mount(dialog);
  await expect(page.getByTestId(dialogIds.save)).toHaveCount(0);

  await page.getByTestId(ids.fieldsShowPoints).check();

  await expect.poll(() => puts.length).toBe(1);
  const sent = puts[0];
  expect(sent.settings.cardFields[0].showPoints).toBe(true);
  expect(sent.settings.cardFields[1]).toEqual(setting('story', ['state']));
  expect(sent.basedOn.cardFields[0].showPoints).toBe(false);
});

test('TaskboardSettings_WhenFieldAddedAndRemoved_ShouldPutEachList', async ({ mount, page }) => {
  const puts = await stub(page, board([setting('task', ['parent'])]));
  await mount(dialog);

  await page.getByTestId(ids.fieldsAdd).selectOption({ label: 'Priority' });
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.cardFields[0].extraFields).toEqual(['parent', 'priority']);
  await expect(page.getByTestId(ids.fieldsExtraCount)).toContainText('2 / 10');

  await page.getByTestId(ids.fieldsExtraRemove).first().click();
  await expect.poll(() => puts.length).toBe(2);
  expect(puts[1].settings.cardFields[0].extraFields).toEqual(['priority']);
});

test('TaskboardSettings_WhenTypeTabChosen_ShouldEditThatTypesFields', async ({ mount, page }) => {
  const puts = await stub(page, board([setting('task', []), setting('story', ['state'])]));
  await mount(dialog);

  await page.locator(`[data-testid="${ids.fieldsTypeTab}"][data-type="story"]`).click();
  await page.getByTestId(ids.fieldsShowKey).uncheck();

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.cardFields[0].showKey).toBe(true);
  expect(puts[0].settings.cardFields[1].showKey).toBe(false);
});

test('TaskboardSettings_WhenTenExtras_ShouldSayFullAndRefuseMore', async ({ mount, page }) => {
  const puts = await stub(page, board([setting('task', TEN)]));
  await mount(dialog);

  await expect(page.getByTestId(ids.fieldsExtraCount)).toContainText('10 / 10');
  await expect(page.getByTestId(ids.fieldsFull)).toBeVisible();
  await expect(page.getByTestId(ids.fieldsAdd)).toBeDisabled();
  await page.getByTestId(ids.fieldsExtraRemove).last().click();
  await expect(page.getByTestId(ids.fieldsFull)).toHaveCount(0);
  await expect(page.getByTestId(ids.fieldsExtraCount)).toContainText('9 / 10');
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.cardFields[0].extraFields).toHaveLength(9);
});

test('TaskboardSettings_WhenTaskTab_ShouldNotOfferStateAsAnExtra', async ({ mount, page }) => {
  await stub(page, board([setting('task', [])]));
  await mount(dialog);

  await expect(page.getByTestId(ids.fieldsAdd).locator('option[value="state"]')).toHaveCount(0);
  await expect(page.getByTestId(ids.fieldsAdd).locator('option[value="priority"]')).toHaveCount(1);
});

test('TaskboardSettings_WhenStoryTab_ShouldNotOfferTagsAndShouldOfferProgress', async ({
  mount,
  page,
}) => {
  await stub(page, board([setting('task', []), setting('story', [])]));
  await mount(dialog);
  await expect(page.getByTestId(ids.fieldsShowTags)).toBeVisible();
  await expect(page.getByTestId(ids.fieldsProgress)).toHaveCount(0);

  await page.locator(`[data-testid="${ids.fieldsTypeTab}"][data-type="story"]`).click();

  await expect(page.getByTestId(ids.fieldsShowTags)).toHaveCount(0);
  await expect(page.getByTestId(ids.fieldsProgress)).toBeChecked();
});

test('TaskboardSettings_WhenProgressToggled_ShouldPutTheTasksAnnotation', async ({
  mount,
  page,
}) => {
  const puts = await stub(page, board([setting('task', []), setting('story', [])]));
  await mount(dialog);
  await page.locator(`[data-testid="${ids.fieldsTypeTab}"][data-type="story"]`).click();

  await page.getByTestId(ids.fieldsProgress).uncheck();

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.annotations.tasks).toBe(false);
  expect(puts[0].basedOn.annotations.tasks).toBe(true);
});

test('TaskboardSettings_WhenEveryFieldAdded_ShouldSayAllAreShown', async ({ mount, page }) => {
  const all = ['priority', 'sprint', 'parent', 'createdAt', 'createdBy', 'changedAt', 'changedBy'];
  await stub(page, board([setting('task', all)]));
  await mount(dialog);

  await expect(page.getByTestId(ids.fieldsAdd)).toBeDisabled();
  await expect(page.getByTestId(ids.fieldsAllAdded)).toBeVisible();
});

test('TaskboardSettings_WhenOverLimit_ShouldSayHowManyToRemove', async ({ mount, page }) => {
  await stub(page, board([setting('task', [...TEN, 'extra'])]));
  await mount(dialog);

  await expect(page.getByTestId(ids.fieldsOverLimit)).toContainText('remove 1.');
});
