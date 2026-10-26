import { expect, test } from '@playwright/experimental-ct-react';
import type { ParentRefDto } from '../../../../api/workItems';
import { feature, stubFeature } from '../../../feature/ct/stubFeature';
import { priya, story } from '../../../workItem/ct/fixtures';
import { stubApi } from '../../../workItem/ct/stubApi';
import { stubComments } from '../../../workItem/ct/stubDetails';
import { stubSide } from '../../../workItem/ct/stubSide';
import { WorkItemFormHarness } from '../../../workItem/ct/WorkItemFormHarness';
import { workItemTestIds } from '../../../workItem/testIds';

// stubComments answers /api/me with the user it is given.
const storageKey = `shiplean.quickSearch.recent.${priya.id}`;
const route = (key: string) => ({ hooksConfig: { route: `/p/SHIP/items/${key}` } });

type StubbedPage = Parameters<typeof stubApi>[0] &
  Parameters<typeof stubSide>[0] &
  Parameters<typeof stubComments>[0] &
  Parameters<typeof stubFeature>[0];

async function stubAll(page: StubbedPage, item: Parameters<typeof stubApi>[1]) {
  await stubApi(page, item);
  await stubSide(page, priya, [], []);
  await stubComments(page, priya, []);
  await stubFeature(page);
}

const parsed = (raw: string | null) => JSON.parse(raw ?? '[]') as unknown;

test('RememberItem_WhenAnItemLoads_ShouldStoreItFirst', async ({ mount, page }) => {
  const parent: ParentRefDto = {
    id: 'w100',
    key: 'SHIP-100',
    type: { id: 'feature', name: 'Feature' },
    title: 'Checkout',
  };
  await stubAll(page, { ...story, parent });
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-141" />, route('SHIP-141'));
  await expect(screen.getByTestId(workItemTestIds.title)).toHaveValue('Pay with saved card');

  // Fails if the hook never runs, stores before the user id is known (under another key), or stores extra fields.
  await expect
    .poll(async () => parsed(await page.evaluate((k) => localStorage.getItem(k), storageKey)))
    .toEqual([
      {
        key: story.key,
        title: story.title,
        type: story.type.id,
        state: story.state.name,
        stateCategory: story.state.category,
        parentPath: [parent.key],
        projectKey: 'SHIP',
      },
    ]);
});

test('RememberItem_WhenAFeatureLoads_ShouldStoreItToo', async ({ mount, page }) => {
  await stubAll(page, feature);
  await mount(<WorkItemFormHarness itemKey={feature.key} />, route(feature.key));

  // Fails if the hook sits only on the plain form's layout and not on features.
  await expect
    .poll(async () => parsed(await page.evaluate((k) => localStorage.getItem(k), storageKey)))
    .toEqual([
      {
        key: feature.key,
        title: feature.title,
        type: feature.type.id,
        state: feature.state.name,
        stateCategory: feature.state.category,
        parentPath: [],
        projectKey: 'SHIP',
      },
    ]);
});
