import { expect, test } from '@playwright/experimental-ct-react';
import { boardTestIds as ids } from '../testIds';
import { GridHarness } from '../ct/GridHarness';
import { card, cell, lane, SETTINGS, storiesBoard } from '../ct/fixtures';

// A board whose only populated cell holds a Blocked card (the board named the rule on it and coloured its tag) and a plain one.
const view = () =>
  storiesBoard({
    lanes: [
      lane({
        id: 'lane-default',
        name: 'Default lane',
        cells: [
          cell('col-new', [
            card({
              key: 'SHIP-129',
              title: 'Sign in with passkey',
              styleRuleId: 'rule-blocked',
              tags: [
                { name: 'Mobile', colour: 'info' },
                { name: 'Blocked', colour: 'danger' },
                { name: 'Loose', colour: null },
              ],
            }),
            card({ key: 'SHIP-130', title: 'Plain card' }),
          ]),
          cell('col-ready'),
          cell('col-ready', [], { doneHalf: true }),
          cell('col-prog'),
          cell('col-done'),
        ],
      }),
    ],
  });

const cardKeyed = (page: Parameters<Parameters<typeof test>[2]>[0]['page'], key: string) =>
  page
    .getByTestId(ids.card)
    .filter({ has: page.getByTestId(ids.cardKey).filter({ hasText: key }) });

test('Styles_WhenCardCarriesRule_ShouldTintAndBoldOnlyThatCard', async ({ mount, page }) => {
  await mount(<GridHarness view={view()} />);
  const blocked = cardKeyed(page, 'SHIP-129');
  await expect(blocked).toHaveAttribute('data-rule', 'rule-blocked');
  await expect(blocked).toHaveCSS('box-shadow', /inset/);
  await expect(blocked.getByTestId(ids.cardTitle)).toHaveCSS('font-weight', '600');

  const plain = cardKeyed(page, 'SHIP-130');
  await expect(plain).not.toHaveAttribute('data-rule', /.+/);
  await expect(plain).not.toHaveCSS('box-shadow', /inset/);
  await expect(plain.getByTestId(ids.cardTitle)).not.toHaveCSS('font-weight', '600');
});

test('Styles_WhenRuleHasNoColour_ShouldOnlyChangeTheTitle', async ({ mount, page }) => {
  const rules = SETTINGS.styleRules.map((rule) => ({ ...rule, colour: null, italic: true }));
  await mount(<GridHarness view={{ ...view(), styleRules: rules }} />);
  const blocked = cardKeyed(page, 'SHIP-129');
  await expect(blocked).not.toHaveCSS('box-shadow', /inset/);
  await expect(blocked.getByTestId(ids.cardTitle)).toHaveCSS('font-style', 'italic');
});

test('Styles_WhenTagsHaveColours_ShouldNameThemAndLeaveUncolouredNeutral', async ({
  mount,
  page,
}) => {
  await mount(<GridHarness view={view()} />);
  const tags = cardKeyed(page, 'SHIP-129').getByTestId(ids.cardTag);
  await expect(tags).toHaveText(['Mobile', 'Blocked', 'Loose']);
  await expect(tags.nth(0)).toHaveAttribute('data-colour', 'info');
  await expect(tags.nth(1)).toHaveAttribute('data-colour', 'danger');
  await expect(tags.nth(2)).toHaveAttribute('data-colour', 'none');
  const [danger, none] = await Promise.all([
    tags.nth(1).evaluate((element) => getComputedStyle(element).backgroundColor),
    tags.nth(2).evaluate((element) => getComputedStyle(element).backgroundColor),
  ]);
  expect(danger).not.toBe(none);
});
