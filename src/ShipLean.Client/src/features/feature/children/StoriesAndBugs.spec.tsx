import { expect, test } from '@playwright/experimental-ct-react';
import { priya } from '../../workItem/ct/fixtures';
import { stubApi } from '../../workItem/ct/stubApi';
import { stubComments } from '../../workItem/ct/stubDetails';
import { stubSide } from '../../workItem/ct/stubSide';
import { workItemHarnessIds as harness } from '../../workItem/ct/harnessIds';
import { WorkItemFormHarness } from '../../workItem/ct/WorkItemFormHarness';
import { feature, stubFeature } from '../ct/stubFeature';
import { featureTestIds as ids } from '../testIds';

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-100' } };
const done = { id: 's-done', name: 'Done', category: 'completed' as const };

type StubbedPage = Parameters<typeof stubApi>[0] &
  Parameters<typeof stubSide>[0] &
  Parameters<typeof stubComments>[0] &
  Parameters<typeof stubFeature>[0];

async function stubAll(page: StubbedPage, item: typeof feature) {
  await stubApi(page, item);
  await stubSide(page, priya, [], []);
  await stubComments(page, priya, []);
  await stubFeature(page);
}

test('StoriesAndBugs_ShouldListOpenFirst', async ({ mount, page }) => {
  const [first, ...rest] = feature.children;
  const item = {
    ...feature,
    children: [
      { ...first!, key: 'SHIP-901', state: done },
      { ...first!, key: 'SHIP-902', id: 'c902', type: { id: 'bug' as const, name: 'Bug' } },
      ...rest,
    ],
  };
  await stubAll(page, item);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  await expect(screen.getByTestId(ids.childRow)).toHaveCount(item.children.length);
  const keys = await screen.getByTestId(ids.childKey).allTextContents();
  expect(keys.indexOf('SHIP-902')).toBeLessThan(keys.indexOf('SHIP-901'));
  expect(keys[keys.length - 1]).toBe('SHIP-901');
  await expect(screen.getByTestId(ids.childrenCount)).toContainText('1/');
});

test('StoriesAndBugs_WhenNone_ShouldShowEmptyState', async ({ mount, page }) => {
  await stubAll(page, { ...feature, children: [] });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  await expect(screen.getByTestId(ids.childrenEmpty)).toBeVisible();
  await expect(screen.getByTestId(ids.childRow)).toHaveCount(0);
});

test('StoriesAndBugs_WhenChildClicked_ShouldOpenChildModal', async ({ mount, page }) => {
  await stubAll(page, feature);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  const child = feature.children[0]!;

  await screen.getByTestId(ids.childTitle).first().click();

  await expect(screen.getByTestId(harness.location)).toHaveText('/p/SHIP/items/SHIP-100');
  await expect(screen.getByTestId(harness.search)).toHaveText(`?item=${child.key}`);
});

test('StoriesAndBugs_ShouldLinkToTheFeatureBoard', async ({ mount, page }) => {
  await stubAll(page, feature);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  const link = screen.getByTestId(ids.taskBoardLink);
  await expect(link).toHaveText('Task board');
  await link.click();

  await expect(screen.getByTestId(harness.location)).toHaveText('/p/SHIP/items/SHIP-100/board');
});
