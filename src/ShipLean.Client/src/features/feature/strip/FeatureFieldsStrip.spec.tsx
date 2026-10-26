import { expect, test } from '@playwright/experimental-ct-react';
import { priya } from '../../workItem/ct/fixtures';
import { stubApi } from '../../workItem/ct/stubApi';
import { stubComments } from '../../workItem/ct/stubDetails';
import { stubSide } from '../../workItem/ct/stubSide';
import { WorkItemFormHarness } from '../../workItem/ct/WorkItemFormHarness';
import { feature, stubFeature } from '../ct/stubFeature';
import { featureTestIds as ids } from '../testIds';
import { closedCount } from '../children/storiesAndBugs';

test('FeatureStrip_ShouldShowSlugStartedAndClosedCount', async ({ mount, page }) => {
  const item = { ...feature, slug: 'checkout-v2', startedLabel: 'Mon 21 Sep' };
  await stubApi(page, item);
  await stubSide(page, priya, [], []);
  await stubComments(page, priya, []);
  await stubFeature(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, {
    hooksConfig: { route: '/p/SHIP/items/SHIP-100' },
  });

  const { closed, total } = closedCount(item.children);
  const strip = screen.getByTestId(ids.strip);
  await expect(strip.getByTestId(ids.stripSlug)).toHaveText('checkout-v2');
  await expect(strip.getByTestId(ids.stripStarted)).toHaveText('Mon 21 Sep');
  await expect(strip.getByTestId(ids.stripChildrenCount)).toHaveText(
    `${closed} of ${total} closed`,
  );
  await expect(strip.getByTestId(ids.stripEpic)).toBeVisible();
});
