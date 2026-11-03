import { expect, test } from '@playwright/experimental-ct-react';
import { retroSharedTestIds as shared } from '../../retroShared/testIds';
import { retroItem } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { ItemFailureLogs } from './ItemFailureLogs';

const address = { retro: undefined, decide: false, point: undefined };
const log = { blockers: ['Port 5432 was taken'], assumptionsDisproved: [], friction: ['Slow CT'] };
const withLog = { ...retroItem, cards: [{ ...retroItem.cards[0], failureLog: log }] };

test('ItemFailureLogs_WhenARetroHasALog_ShouldShowItCollapsedUntilOpenedAndNeverEditable', async ({
  mount,
  page,
}) => {
  await mount(<ItemFailureLogs item={withLog} address={address} />);
  await expect(page.getByTestId(shared.retroCardLogToggle)).toContainText(
    'verbatim from the agent',
  );
  await expect(page.getByTestId(shared.retroCardLog)).toHaveCount(0);
  await page.getByTestId(shared.retroCardLogToggle).click();
  await expect(page.getByTestId(shared.retroCardLog)).toContainText('Port 5432 was taken');
  await expect(
    page.getByTestId(ids.retroItemLogs).locator('textarea, input, button:not([aria-expanded])'),
  ).toHaveCount(0);
});

test('ItemFailureLogs_WhenNoRetroHasALog_ShouldRenderNothing', async ({ mount, page }) => {
  await mount(<ItemFailureLogs item={retroItem} address={address} />);
  await expect(page.getByTestId(ids.retroItemLogs)).toHaveCount(0);
});

test('ItemFailureLogs_WhenARetroIsPicked_ShouldShowOnlyThatRetrosLog', async ({ mount, page }) => {
  const [first] = withLog.cards;
  const two = { ...withLog, cards: [first, { ...first, id: 'second', failureLog: log }] };
  await mount(<ItemFailureLogs item={two} address={{ ...address, retro: 'second' }} />);
  await expect(page.getByTestId(shared.retroCardLogToggle)).toHaveCount(1);
});
