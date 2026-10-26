import { expect, test } from '@playwright/experimental-ct-react';
import type { StyleRuleJson } from '../../../api/persisted';
import { boardTestIds as cardIds } from '../../board/testIds';
import { settingsDialogTestIds as dialogIds } from '../../settingsDialog/testIds';
import { BoardSettingsDialog } from '../BoardSettingsDialog';
import { boardSettingsDto, emptySettings, PROJECT_ID } from '../ct/boardSettingsFixtures';
import { boardSettingsTestIds as ids } from '../testIds';
import { styleRule, stubBoard } from '../ct/stubBoard';

const blocked = styleRule('blocked', 'Blocked', {
  field: 'tags',
  op: 'contains',
  value: 'Blocked',
});
const urgent = styleRule('urgent', 'Urgent', { field: 'priority', op: '=', value: 1 });

const board = (styleRules: StyleRuleJson[]) =>
  boardSettingsDto({ settings: { ...emptySettings(), styleRules } });

const mountStyles = (mount: (component: React.JSX.Element) => Promise<unknown>) =>
  mount(
    <BoardSettingsDialog
      projectId={PROJECT_ID}
      level="stories"
      section="styles"
      onSectionChange={() => undefined}
      onClose={() => undefined}
    />,
  );

test('StylesPane_WhenTwoRulesMatch_ShouldPreviewFirstAndNameTheOther', async ({ mount, page }) => {
  await stubBoard(page, board([blocked, urgent]));
  await mountStyles(mount);

  const card = page.getByTestId(cardIds.card).filter({ hasText: 'KEY-129' });
  await expect(card).toHaveAttribute('data-rule', 'blocked');
  await expect(page.getByTestId(ids.stylesPreviewCaption).first()).toContainText(
    'Blocked wins. Also matches Urgent',
  );
});

test('StylesPane_WhenRuleMovedUp_ShouldPreviewTheNewFirstAndPutTheOrder', async ({
  mount,
  page,
}) => {
  const puts = await stubBoard(page, board([blocked, urgent]));
  await mountStyles(mount);
  const card = page.getByTestId(cardIds.card).filter({ hasText: 'KEY-129' });

  await page.getByTestId(ids.styleGrip).nth(1).focus();
  await page.keyboard.press('Alt+ArrowUp');

  await expect(page.getByTestId(ids.styleName).first()).toHaveValue('Urgent');
  await expect(card).toHaveAttribute('data-rule', 'urgent');
  await page.getByTestId(dialogIds.save).click();
  await expect(page.getByTestId(ids.styleRow)).toHaveCount(2);
  expect(puts).toHaveLength(1);
  expect(puts[0].settings.styleRules.map((r) => r.id)).toEqual(['urgent', 'blocked']);
  expect(puts[0].basedOn.styleRules.map((r) => r.id)).toEqual(['blocked', 'urgent']);
});

test('StylesPane_WhenRuleDraggedAboveTheFirst_ShouldPreviewTheNewFirstAndPutTheOrder', async ({
  mount,
  page,
}) => {
  const puts = await stubBoard(page, board([blocked, urgent]));
  await mountStyles(mount);
  const card = page.getByTestId(cardIds.card).filter({ hasText: 'KEY-129' });
  await expect(card).toHaveAttribute('data-rule', 'blocked');

  await page.getByTestId(ids.styleGrip).nth(1).dragTo(page.getByTestId(ids.styleRow).first());

  await expect(page.getByTestId(ids.styleName).first()).toHaveValue('Urgent');
  await expect(card).toHaveAttribute('data-rule', 'urgent');
  await page.getByTestId(dialogIds.save).click();
  await expect(page.getByTestId(ids.styleRow)).toHaveCount(2);
  expect(puts[0].settings.styleRules.map((r) => r.id)).toEqual(['urgent', 'blocked']);
});

test('StylesPane_WhenColourPicked_ShouldNameColoursAsTheDesignDoes', async ({ mount, page }) => {
  const puts = await stubBoard(page, board([blocked]));
  await mountStyles(mount);
  const colour = page.getByTestId(ids.styleColour);
  await expect(colour.locator('option')).toHaveText([
    'No colour',
    'Red',
    'Amber',
    'Green',
    'Blue',
    'Accent',
    'Grey',
    'Purple',
  ]);

  await colour.selectOption({ label: 'Amber' });
  await page.getByTestId(dialogIds.save).click();

  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.styleRules[0].colour).toBe('warn');
});

test('StylesPane_WhenRuleReadsAFieldTheSamplesLack_ShouldSaySoInsteadOfPreviewingIt', async ({
  mount,
  page,
}) => {
  const imported = styleRule('imp', 'Imported', {
    field: 'originalSystem',
    op: 'isEmpty',
    value: null,
  });
  await stubBoard(page, board([imported]));
  await mountStyles(mount);

  await expect(page.getByTestId(ids.stylesPreviewUnpreviewable)).toContainText(
    "Can't preview Imported: it uses Original system",
  );
});

test('StylesPane_WhenFirstRuleDisabled_ShouldFallToTheNextRule', async ({ mount, page }) => {
  await stubBoard(page, board([blocked, urgent]));
  await mountStyles(mount);
  const card = page.getByTestId(cardIds.card).filter({ hasText: 'KEY-129' });

  await page.getByTestId(ids.styleEnabled).first().uncheck();

  await expect(card).toHaveAttribute('data-rule', 'urgent');
});

test('StylesPane_WhenCriteriaEdited_ShouldChangeWhichCardsMatch', async ({ mount, page }) => {
  await stubBoard(page, board([blocked]));
  await mountStyles(mount);
  const spike = page.getByTestId(cardIds.card).filter({ hasText: 'KEY-144' });
  await expect(spike).not.toHaveAttribute('data-rule', 'blocked');

  await page.getByTestId(ids.styleCriteria).getByTestId('criteria-value').fill('Spike');

  await expect(spike).toHaveAttribute('data-rule', 'blocked');
});

test('StylesPane_WhenRuleAddedAndDeleted_ShouldListIt', async ({ mount, page }) => {
  await stubBoard(page, board([]));
  await mountStyles(mount);
  await expect(page.getByTestId(ids.stylesEmpty)).toBeVisible();

  await page.getByTestId(ids.stylesAdd).click();
  await expect(page.getByTestId(ids.styleRow)).toHaveCount(1);
  await expect(page.getByTestId(ids.stylesEmpty)).toHaveCount(0);

  await page.getByTestId(ids.styleDelete).click();
  await expect(page.getByTestId(ids.styleRow)).toHaveCount(0);
});

test('StylesPane_WhenTagColoured_ShouldTintTheTagOnThePreview', async ({ mount, page }) => {
  const dto = boardSettingsDto({
    settings: { ...emptySettings(), tagColours: [{ tag: 'Blocked', colour: 'warn' }] },
  });
  await stubBoard(page, dto);
  await mountStyles(mount);

  const card = page.getByTestId(cardIds.card).filter({ hasText: 'KEY-129' });
  await expect(card.getByTestId(cardIds.cardTag).first()).toHaveAttribute('data-colour', 'warn');
});
