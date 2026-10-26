import { expect, test } from '@playwright/experimental-ct-react';
import { priya } from '../../workItem/ct/fixtures';
import { workItemHarnessIds as harness } from '../../workItem/ct/harnessIds';
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

const epic = {
  id: 'e1',
  key: 'SHIP-90',
  type: { id: 'epic' as const, name: 'Epic' },
  title: 'Payments',
};

type StubbedPage = Parameters<typeof stubApi>[0] &
  Parameters<typeof stubSide>[0] &
  Parameters<typeof stubComments>[0] &
  Parameters<typeof stubFeature>[0];

async function stubWithEpic(page: StubbedPage, title = epic.title) {
  await stubApi(page, { ...feature, parent: { ...epic, title } });
  await stubSide(page, priya, [], []);
  await stubComments(page, priya, []);
  await stubFeature(page);
}

const at = { hooksConfig: { route: '/p/SHIP/items/SHIP-100' } };

test('EpicField_WhenOpenClicked_ShouldOpenEpicModal', async ({ mount, page }) => {
  await stubWithEpic(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, at);

  await screen.getByRole('link', { name: 'Open epic SHIP-90' }).click();

  await expect(screen.getByTestId(harness.location)).toHaveText('/p/SHIP/items/SHIP-100');
  await expect(screen.getByTestId(harness.search)).toHaveText('?item=SHIP-90');
});

test('EpicField_WhenEpicTitleIsVeryLong_ShouldKeepOpenLinkInsideTheCell', async ({
  mount,
  page,
}) => {
  await stubWithEpic(page, 'An epic with a remarkably long title '.repeat(12));
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, at);

  const link = await screen.getByTestId(ids.epicOpen).boundingBox();
  const cell = await screen.locator('[data-field="parent"]').boundingBox();
  expect(link).not.toBeNull();
  expect(cell).not.toBeNull();
  expect(link!.x).toBeGreaterThanOrEqual(cell!.x);
  expect(link!.x + link!.width).toBeLessThanOrEqual(cell!.x + cell!.width + 1);
});
