import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { boardTestIds as cardIds } from '../../board/testIds';
import { settingsDialogTestIds as dialogIds } from '../../settingsDialog/testIds';
import { BoardSettingsDialog } from '../BoardSettingsDialog';
import { boardSettingsDto, emptySettings, PROJECT_ID } from '../ct/boardSettingsFixtures';
import { boardSettingsTestIds as ids } from '../testIds';
import { styleRule, stubBoard } from '../ct/stubBoard';

const mountTags = (mount: (component: React.JSX.Element) => Promise<unknown>) =>
  mount(
    <BoardSettingsDialog
      projectId={PROJECT_ID}
      level="stories"
      section="tags"
      onSectionChange={() => undefined}
      onClose={() => undefined}
    />,
  );

const row = (page: Page, tag: string) => page.getByTestId(ids.tagRow).filter({ hasText: tag });

// SWATCHES order: danger, warn, ok, ...
const OK = 2;

test('TagColoursPane_WhenSwatchPicked_ShouldTintThePreviewAndPutIt', async ({ mount, page }) => {
  const puts = await stubBoard(page, boardSettingsDto(), ['Blocked', 'Customer']);
  await mountTags(mount);
  await expect(page.getByTestId(ids.tagRow)).toHaveCount(2);

  await row(page, 'Blocked').getByTestId(ids.tagSwatch).nth(OK).click();

  const preview = page.getByTestId(cardIds.card).filter({ hasText: 'KEY-129' });
  await expect(preview.getByTestId(cardIds.cardTag).first()).toHaveAttribute('data-colour', 'ok');
  await page.getByTestId(dialogIds.save).click();
  await expect(page.getByTestId(ids.tagRow)).toHaveCount(2);
  expect(puts).toHaveLength(1);
  expect(puts[0].settings.tagColours).toEqual([{ tag: 'Blocked', colour: 'ok' }]);
  expect(puts[0].settings.styleRules).toEqual(puts[0].basedOn.styleRules);
});

test('TagColoursPane_WhenColourCleared_ShouldDropTheEntry', async ({ mount, page }) => {
  const dto = boardSettingsDto({
    settings: { ...emptySettings(), tagColours: [{ tag: 'Blocked', colour: 'warn' }] },
  });
  const puts = await stubBoard(page, dto, ['Blocked']);
  await mountTags(mount);

  await row(page, 'Blocked').getByTestId(ids.tagClear).click();
  await page.getByTestId(dialogIds.save).click();

  await expect(page.getByTestId(ids.tagRow)).toHaveCount(1);
  expect(puts[0].settings.tagColours).toEqual([]);
});

test('TagColoursPane_WhenColouredTagIsUnused_ShouldStillListIt', async ({ mount, page }) => {
  const dto = boardSettingsDto({
    settings: { ...emptySettings(), tagColours: [{ tag: 'Gone', colour: 'info' }] },
  });
  await stubBoard(page, dto, ['Blocked']);
  await mountTags(mount);

  await expect(page.getByTestId(ids.tagRow)).toHaveCount(2);
  await expect(row(page, 'Gone')).toBeVisible();
});

test('TagColoursPane_WhenTagsDifferInCase_ShouldShowTheFirstEntrysColourOnBoth', async ({
  mount,
  page,
}) => {
  const dto = boardSettingsDto({
    settings: {
      ...emptySettings(),
      tagColours: [
        { tag: 'blocked', colour: 'ok' },
        { tag: 'BLOCKED', colour: 'warn' },
      ],
    },
  });
  await stubBoard(page, dto, ['Blocked', 'blocked']);
  await mountTags(mount);

  await expect(page.getByTestId(ids.tagRow)).toHaveCount(2);
  await expect(page.getByTestId(ids.tagRow).first()).toContainText('Green');
  await expect(page.getByTestId(ids.tagRow).last()).toContainText('Green');
});

test('TagColoursPane_WhenNoTags_ShouldSayNoTagYet', async ({ mount, page }) => {
  await stubBoard(page, boardSettingsDto(), []);
  await mountTags(mount);

  await expect(page.getByTestId(ids.tagsEmpty)).toBeVisible();
});

test('TagColoursPane_WhenStyleRuleMatches_ShouldShowItOnThePreview', async ({ mount, page }) => {
  const dto = boardSettingsDto({
    settings: {
      ...emptySettings(),
      styleRules: [
        styleRule('blocked', 'Blocked', { field: 'tags', op: 'contains', value: 'Blocked' }),
      ],
    },
  });
  await stubBoard(page, dto, ['Blocked']);
  await mountTags(mount);

  await expect(page.getByTestId(cardIds.card).filter({ hasText: 'KEY-129' })).toHaveAttribute(
    'data-rule',
    'blocked',
  );
});
