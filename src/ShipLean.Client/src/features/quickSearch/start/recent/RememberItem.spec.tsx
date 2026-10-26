import { expect, test } from '@playwright/experimental-ct-react';
import { meDto } from '../../../shell/shellFixtures';
import { feature, stubFeature } from '../../../feature/ct/stubFeature';
import { priya, story } from '../../../workItem/ct/fixtures';
import { stubApi } from '../../../workItem/ct/stubApi';
import { stubComments } from '../../../workItem/ct/stubDetails';
import { stubSide } from '../../../workItem/ct/stubSide';
import { WorkItemFormHarness } from '../../../workItem/ct/WorkItemFormHarness';
import { workItemTestIds } from '../../../workItem/testIds';

const me = meDto();
const storageKey = `shiplean.quickSearch.recent.${me.id}`;
const route = (key: string) => ({ hooksConfig: { route: `/p/SHIP/items/${key}` } });

type StubbedPage = Parameters<typeof stubApi>[0] &
  Parameters<typeof stubSide>[0] &
  Parameters<typeof stubComments>[0] &
  Parameters<typeof stubFeature>[0];

async function stubAll(page: StubbedPage, item: Parameters<typeof stubApi>[1]) {
  await page.route('**/api/me', (r) =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(me) }),
  );
  await stubApi(page, item);
  await stubSide(page, priya, [], []);
  await stubComments(page, priya, []);
  await stubFeature(page);
}

const storedKeys = (raw: string | null) =>
  (JSON.parse(raw ?? '[]') as { key: string; title: string }[]).map((i) => [i.key, i.title]);

test('RememberItem_WhenAnItemLoads_ShouldStoreItFirst', async ({ mount, page }) => {
  await stubAll(page, story);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));
  await expect(screen.getByTestId(workItemTestIds.title)).toHaveValue('Pay with saved card');

  // Fails if the hook never runs, or stores before the user id is known (under another key).
  await expect
    .poll(async () => storedKeys(await page.evaluate((k) => localStorage.getItem(k), storageKey)))
    .toEqual([['SHIP-141', 'Pay with saved card']]);
});

test('RememberItem_WhenAFeatureLoads_ShouldStoreItToo', async ({ mount, page }) => {
  await stubAll(page, feature);
  await mount(<WorkItemFormHarness itemKey={feature.key} />, route(feature.key));

  // Fails if the hook sits only on the plain form's layout and not on features.
  await expect
    .poll(async () => storedKeys(await page.evaluate((k) => localStorage.getItem(k), storageKey)))
    .toEqual([[feature.key, feature.title]]);
});
