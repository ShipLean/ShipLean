import { expect, test } from '@playwright/experimental-ct-react';
import type { BoardSettingsDto, BoardSettingsPut } from '../../../api/boards';
import type { CardFieldSettingJson } from '../../../api/persisted';
import { boardTestIds as cardIds } from '../../board/testIds';
import { settingsDialogTestIds as dialogIds } from '../../settingsDialog/testIds';
import { BoardSettingsDialog } from '../BoardSettingsDialog';
import { BOARD_ID, boardSettingsDto, emptySettings, PROJECT_ID } from '../ct/boardSettingsFixtures';
import { styleRule } from '../ct/stubBoard';
import { boardSettingsTestIds as ids } from '../testIds';

const setting = (
  type: CardFieldSettingJson['type'],
  extraFields: string[],
  overrides: Partial<CardFieldSettingJson> = {},
): CardFieldSettingJson => ({
  type,
  showKey: true,
  showAssignee: true,
  showTags: true,
  showPoints: true,
  showEmptyFields: false,
  extraFields,
  ...overrides,
});

const board = (cardFields: CardFieldSettingJson[]): BoardSettingsDto =>
  boardSettingsDto({ settings: { ...emptySettings(), cardFields } });

async function stub(
  page: {
    route(
      url: string,
      handler: (route: {
        request(): { method(): string; postDataJSON(): unknown };
        fulfill(response: { json: unknown; status?: number }): Promise<void>;
      }) => Promise<void>,
    ): Promise<unknown>;
  },
  dto: BoardSettingsDto,
): Promise<BoardSettingsPut[]> {
  const puts: BoardSettingsPut[] = [];
  await page.route(`**/api/projects/${PROJECT_ID}/boards`, (route) =>
    route.fulfill({ json: [{ id: BOARD_ID, level: dto.level, name: dto.level }] }),
  );
  await page.route(`**/api/boards/${BOARD_ID}/settings`, async (route) => {
    if (route.request().method() === 'GET') return route.fulfill({ json: dto });
    const body = route.request().postDataJSON() as BoardSettingsPut;
    puts.push(body);
    return route.fulfill({
      json: { board: { ...dto, settings: body.settings }, mergedSections: [] },
    });
  });
  return puts;
}

const mountFields = (mount: (component: React.JSX.Element) => Promise<unknown>) =>
  mount(
    <BoardSettingsDialog
      projectId={PROJECT_ID}
      level="stories"
      section="fields"
      onSectionChange={() => undefined}
      onClose={() => undefined}
    />,
  );

const storiesBoard = () =>
  board([setting('story', ['parent', 'priority']), setting('bug', ['parent', 'severity'])]);

test('FieldsPane_WhenFieldAdded_ShouldShowItOnThePreviewAndPutIt', async ({ mount, page }) => {
  const puts = await stub(page, storiesBoard());
  await mountFields(mount);
  const preview = page.getByTestId(cardIds.cardExtra);
  await expect(preview).toHaveCount(1);

  await page.getByTestId(ids.fieldsAdd).selectOption({ label: 'Changed by' });

  await expect(preview).toHaveCount(2);
  await expect(preview.last()).toContainText('Changed by');
  await expect(page.getByTestId(ids.fieldsExtraCount)).toContainText('3 / 10');
  await page.getByTestId(dialogIds.save).click();
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.cardFields[0].extraFields).toEqual(['parent', 'priority', 'changedBy']);
});

test('FieldsPane_WhenRulesAndTagColoursSet_ShouldApplyThemToThePreview', async ({
  mount,
  page,
}) => {
  const dto = boardSettingsDto({
    settings: {
      ...emptySettings(),
      cardFields: [setting('story', ['parent']), setting('bug', [])],
      styleRules: [styleRule('mid', 'Mid', { field: 'priority', op: '=', value: 2 })],
      tagColours: [{ tag: 'feedback', colour: 'ok' }],
    },
  });
  await stub(page, dto);
  await mountFields(mount);

  const card = page.getByTestId(ids.fieldsPreview).getByTestId(cardIds.card);
  await expect(card).toHaveAttribute('data-rule', 'mid');
  await expect(card.getByTestId(cardIds.cardTag).first()).toHaveAttribute('data-colour', 'ok');
});

test('FieldsPane_WhenCoreFieldOff_ShouldLeaveItOffThePreview', async ({ mount, page }) => {
  await stub(page, storiesBoard());
  await mountFields(mount);
  const key = page.getByTestId(cardIds.cardKey);
  await expect(key).toHaveText('KEY-58');

  await page.getByTestId(ids.fieldsShowKey).uncheck();

  await expect(key).toHaveCount(0);
});

test('FieldsPane_WhenShowEmptyToggled_ShouldShowEmptyFieldsAsDash', async ({ mount, page }) => {
  await stub(page, board([setting('story', ['sprint', 'priority']), setting('bug', [])]));
  await mountFields(mount);
  const fields = page.getByTestId(cardIds.cardExtra);
  await expect(fields).toHaveCount(1);
  await expect(fields).not.toContainText('Sprint');
  await expect(page.getByTestId(cardIds.cardPoints)).toHaveText('?');
  await expect(page.getByTestId(cardIds.cardAssignee)).toHaveAttribute('data-assigned', 'false');

  await page.getByTestId(ids.fieldsShowEmpty).check();

  await expect(fields).toHaveCount(2);
  await expect(fields.first()).toContainText('Sprint');
  await expect(fields.first()).toContainText('—');
});

test('FieldsPane_WhenTypeChosen_ShouldEditThatTypesFieldsOnly', async ({ mount, page }) => {
  const puts = await stub(page, storiesBoard());
  await mountFields(mount);
  const offered = () =>
    page
      .getByTestId(ids.fieldsAdd)
      .evaluate((select: HTMLSelectElement) =>
        Array.from(select.options).map((option) => option.value),
      );
  expect(await offered()).not.toContain('severity');
  expect(await offered()).not.toContain('state');

  await page.getByTestId(ids.fieldsTypeTab).nth(1).click();
  await expect(page.getByTestId(ids.fieldsExtraRow)).toHaveCount(2);
  await page.getByTestId(ids.fieldsExtraRemove).first().click();
  await page.getByTestId(dialogIds.save).click();

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.cardFields[0].extraFields).toEqual(['parent', 'priority']);
  expect(puts[0].settings.cardFields[1].extraFields).toEqual(['severity']);
});

test('FieldsPane_WhenTenExtras_ShouldRefuseAnEleventh', async ({ mount, page }) => {
  const ten = Array.from({ length: 10 }, (_, i) => `field${i}`);
  await stub(page, board([setting('bug', ten)]));
  await mountFields(mount);

  await expect(page.getByTestId(ids.fieldsExtraCount)).toContainText('10 / 10');
  await expect(page.getByTestId(ids.fieldsAdd)).toBeDisabled();
  await expect(page.getByTestId(ids.fieldsFull)).toContainText('at most 10');

  await page.getByTestId(ids.fieldsExtraRemove).first().click();
  await expect(page.getByTestId(ids.fieldsAdd)).toBeEnabled();
  await expect(page.getByTestId(ids.fieldsFull)).toHaveCount(0);
});

test('FieldsPane_WhenMoreThanTenStored_ShouldSayHowManyToRemove', async ({ mount, page }) => {
  const eleven = Array.from({ length: 11 }, (_, i) => `field${i}`);
  await stub(page, board([setting('bug', eleven)]));
  await mountFields(mount);

  await expect(page.getByTestId(ids.fieldsOverLimit)).toContainText('remove 1 to save');
  await page.getByTestId(ids.fieldsExtraRemove).first().click();
  await expect(page.getByTestId(ids.fieldsOverLimit)).toHaveCount(0);
});

test('FieldsPane_WhenMovedUp_ShouldReorderPreview', async ({ mount, page }) => {
  await stub(page, board([setting('story', ['priority', 'changedBy'])]));
  await mountFields(mount);
  const fields = page.getByTestId(cardIds.cardExtra);
  await expect(fields.first()).toContainText('Priority');

  await page.getByTestId(ids.fieldsExtraUp).nth(1).click();

  await expect(fields.first()).toContainText('Changed by');
});

test('FieldsPane_WhenParentAndStateListed_ShouldMarkThemAndLeaveTheParentLineOnTheCard', async ({
  mount,
  page,
}) => {
  await stub(page, board([setting('story', ['parent', 'state', 'priority'])]));
  await mountFields(mount);
  const rows = page.getByTestId(ids.fieldsExtraRow);
  await expect(rows.nth(0)).toContainText('always on the card');
  await expect(rows.nth(1)).toContainText('shown by the column');
  await expect(page.getByTestId(cardIds.cardExtra)).toHaveCount(1);
  await expect(page.getByTestId(cardIds.cardParent)).toBeVisible();

  await page.getByTestId(ids.fieldsExtraRemove).first().click();

  await expect(page.getByTestId(cardIds.cardParent)).toBeVisible();
});
