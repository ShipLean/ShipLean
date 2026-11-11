import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { DecisionDto, ItemAsksDto } from '@shiplean/core/api/asks';
import { componentTestIds } from '../../components/testIds';
import { meDto, stubShell } from '../shell/shellFixtures';
import { feature, waiting } from './ct/fixtures';
import { RegionHarness } from './ct/RegionHarness';
import { stubAsks } from './ct/stubAsks';
import type { AsksItem } from './regions';
import { asksTestIds as ids } from './testIds';
import { DecisionChange } from './DecisionChange';

const itemFor = (asks: ItemAsksDto): AsksItem => ({
  id: asks.workItemId,
  projectId: 'p1',
  projectKey: 'SHIP',
  key: 'SHIP-141',
  type: { id: 'story', name: 'Story' },
});
const decisionOf = (asks: ItemAsksDto, number: number): DecisionDto => {
  const found = asks.decisions.find((decision) => decision.number === number);
  if (!found) throw new Error(`No D${number} in the fixture`);
  return found;
};
const me = meDto({ name: 'Priya Shah' });

// D1 is open, D5 confirmed (recommended), D6 changed (alternative); the feature's D1 is changed to an own answer.
async function setUp(page: Page, asks: ItemAsksDto, number: number) {
  await stubShell(page, { me });
  const stub = await stubAsks(page, asks);
  const closed = { count: 0 };
  const view = (
    <RegionHarness>
      <DecisionChange
        item={itemFor(asks)}
        asks={asks}
        decision={decisionOf(asks, number)}
        onClose={() => {
          closed.count += 1;
        }}
      />
    </RegionHarness>
  );
  return { stub, closed, view };
}
const toast = (page: Page) => page.getByTestId(componentTestIds.toast);
const escape = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
// A toast reads its message then the dismiss glyph; anchoring both ends keeps a longer message from matching.
const toastText = (message: string) => new RegExp(`^${escape(message)}\\s*×$`);
const path = (asks: ItemAsksDto, rest: string) => `/api/work-items/${asks.workItemId}/${rest}`;

test('DecisionChange_WhenAlternativePicked_ShouldPostAlternativeIndexAndNote', async ({
  mount,
  page,
}) => {
  const { stub, closed, view } = await setUp(page, waiting, 1);
  const component = await mount(view);

  await component.getByTestId(ids.changeOption).nth(1).check();
  await component.getByTestId(ids.changeNote).fill('The picker is a list');
  await component.getByTestId(ids.changeSave).click();

  await expect.poll(() => stub.writes.length).toBe(1);
  expect(stub.writes[0].url).toBe(path(waiting, 'decisions/1/change'));
  expect(stub.writes[0].body).toEqual({
    kind: 'alternative',
    alternativeIndex: 0,
    text: null,
    note: 'The picker is a list',
  });
  await expect(toast(page)).toHaveText(toastText('D1 changed'));
  await expect.poll(() => closed.count).toBe(1);
});

test('DecisionChange_WhenOwnAnswerBlank_ShouldDisableSave', async ({ mount, page }) => {
  const { view } = await setUp(page, waiting, 1);
  const component = await mount(view);
  const save = component.getByTestId(ids.changeSave);

  await expect(save).toBeEnabled();
  await component.getByTestId(ids.changeOption).last().check();
  await expect(save).toBeDisabled();
  await component.getByTestId(ids.changeOwnInput).fill('   ');
  await expect(save).toBeDisabled();
  await component.getByTestId(ids.changeOwnInput).fill('Something else');
  await expect(save).toBeEnabled();
});

test('DecisionChange_WhenOwnAnswerTyped_ShouldPostOwnText', async ({ mount, page }) => {
  const { stub, view } = await setUp(page, waiting, 1);
  const component = await mount(view);

  await component.getByTestId(ids.changeOwnInput).fill('  Saved cards sit in a drawer  ');
  await expect(component.getByTestId(ids.changeOption).last()).toBeChecked();
  await component.getByTestId(ids.changeSave).click();

  await expect.poll(() => stub.writes.length).toBe(1);
  expect(stub.writes[0].url).toBe(path(waiting, 'decisions/1/change'));
  expect(stub.writes[0].body).toEqual({
    kind: 'own',
    alternativeIndex: null,
    text: 'Saved cards sit in a drawer',
    note: null,
  });
});

test('DecisionChange_WhenSettled_ShouldWarnAboutComment', async ({ mount, page }) => {
  const { stub, closed, view } = await setUp(page, waiting, 5);
  const component = await mount(view);

  const warning = component.getByTestId(ids.changeSettledWarning);
  await expect(warning).toContainText('D5 is already confirmed.');
  await expect(warning).toContainText('posts a comment on SHIP-141');
  await expect(warning).toContainText('by Priya Shah');

  await component.getByTestId(ids.changeOption).nth(1).check();
  await component.getByTestId(ids.changeSave).click();
  await expect.poll(() => stub.writes.length).toBe(1);
  expect(stub.writes[0].url).toBe(path(waiting, 'decisions/5/change'));
  await expect(toast(page)).toHaveText(toastText('D5 changed · comment posted for the next agent'));
  await expect.poll(() => closed.count).toBe(1);
});

test('DecisionChange_WhenOpen_ShouldNotWarnAboutComment', async ({ mount, page }) => {
  const { view } = await setUp(page, waiting, 1);
  const component = await mount(view);

  await expect(component.getByTestId(ids.changeOption)).toHaveCount(4);
  await expect(component.getByTestId(ids.changeSettledWarning)).toHaveCount(0);
});

test('DecisionChange_WhenRecommendedOnOpen_ShouldConfirm', async ({ mount, page }) => {
  const { stub, closed, view } = await setUp(page, waiting, 1);
  const component = await mount(view);

  await component.getByTestId(ids.changeSave).click();

  await expect.poll(() => stub.writes.length).toBe(1);
  expect(stub.writes[0].url).toBe(path(waiting, 'decisions/confirm'));
  expect(stub.writes[0].body).toEqual({ numbers: [1] });
  await expect(toast(page)).toHaveText(toastText('D1 confirmed'));
  await expect.poll(() => closed.count).toBe(1);
});

test('DecisionChange_WhenRecommendedOnOpenWithNote_ShouldPostChangeWithNote', async ({
  mount,
  page,
}) => {
  const { stub, view } = await setUp(page, waiting, 1);
  const component = await mount(view);

  await component.getByTestId(ids.changeNote).fill('Checked with Mei');
  await component.getByTestId(ids.changeSave).click();

  await expect.poll(() => stub.writes.length).toBe(1);
  expect(stub.writes[0].url).toBe(path(waiting, 'decisions/1/change'));
  expect(stub.writes[0].body).toEqual({
    kind: 'recommended',
    alternativeIndex: null,
    text: null,
    note: 'Checked with Mei',
  });
  await expect(toast(page)).toHaveText(toastText('D1 confirmed'));
});

test('DecisionChange_WhenCurrentAnswerSavedOnSettled_ShouldCloseWithoutPosting', async ({
  mount,
  page,
}) => {
  const { stub, closed, view } = await setUp(page, waiting, 5);
  const component = await mount(view);

  await expect(component.getByTestId(ids.changeOption).first()).toBeChecked();
  await component.getByTestId(ids.changeSave).click();

  await expect(toast(page)).toHaveText(toastText('D5 unchanged'));
  await expect.poll(() => closed.count).toBe(1);
  expect(stub.writes).toHaveLength(0);
});

test('DecisionChange_WhenAnswerIsOwn_ShouldOfferCurrentAnswerBetweenAlternativesAndOwn', async ({
  mount,
  page,
}) => {
  const asks = feature;
  const d1 = decisionOf(asks, 1);
  const { stub, closed, view } = await setUp(page, asks, 1);
  const component = await mount(view);

  const options = component.getByTestId(ids.changeOption);
  await expect(options).toHaveCount(d1.alternatives.length + 3);
  const current = options.nth(d1.alternatives.length + 1);
  await expect(current).toBeChecked();
  await expect(current.locator('xpath=..')).toContainText(d1.currentAnswer);
  await expect(current.locator('xpath=..')).toContainText('Current answer · current');

  await component.getByTestId(ids.changeSave).click();
  await expect(toast(page)).toHaveText(toastText(`${d1.label} unchanged`));
  await expect.poll(() => closed.count).toBe(1);
  expect(stub.writes).toHaveLength(0);
});

test('DecisionChange_WhenRefused_ShouldShowErrorAndKeepChoice', async ({ mount, page }) => {
  const { stub, closed, view } = await setUp(page, waiting, 1);
  stub.failNext(409, { error: 'D1 is no longer open.', code: 'conflict' });
  const component = await mount(view);

  await component.getByTestId(ids.changeOwnInput).fill('Saved cards sit in a drawer');
  await component.getByTestId(ids.changeNote).fill('Why');
  await component.getByTestId(ids.changeSave).click();

  await expect(component.getByTestId(ids.changeError)).toHaveText('D1 is no longer open.');
  await expect(component.getByTestId(ids.changeOption).last()).toBeChecked();
  await expect(component.getByTestId(ids.changeOwnInput)).toHaveValue(
    'Saved cards sit in a drawer',
  );
  await expect(component.getByTestId(ids.changeNote)).toHaveValue('Why');
  expect(closed.count).toBe(0);
});

const shots: [string, ItemAsksDto, number, (page: Page) => Promise<void>][] = [
  ['change', waiting, 1, (page) => page.getByTestId(ids.changeOption).nth(1).check()],
  [
    'settled-change',
    waiting,
    5,
    async (page) => {
      await page
        .getByTestId(ids.changeOwnInput)
        .fill('The card used last, unless it expires this month');
      await page
        .getByTestId(ids.changeNote)
        .fill('Cards about to expire fail more often at the bank.');
    },
  ],
];
for (const [name, asks, number, prepare] of shots) {
  for (const theme of ['light', 'dark'] as const) {
    test(`DecisionChange_WhenRendered_ShouldMatchItsScreenshot_${name}_${theme}`, async ({
      mount,
      page,
    }) => {
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      const { view } = await setUp(page, asks, number);
      const component = await mount(view);
      await prepare(page);
      if (number === 5) await expect(component.getByTestId(ids.changeSettledWarning)).toBeVisible();
      await expect(component.getByTestId(ids.decisionChange)).toBeVisible();
      await expect(component.getByTestId(ids.decisionChange)).toHaveScreenshot(
        `decision-${name}.${theme}.png`,
        { animations: 'disabled' },
      );
    });
  }
}
