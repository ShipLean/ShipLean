import { expect, test } from '@playwright/experimental-ct-react';
import { priya } from '../../workItem/ct/fixtures';
import { stubApi } from '../../workItem/ct/stubApi';
import { stubComments } from '../../workItem/ct/stubDetails';
import { stubSide } from '../../workItem/ct/stubSide';
import { WorkItemFormHarness } from '../../workItem/ct/WorkItemFormHarness';
import { feature, stubFeature } from '../ct/stubFeature';
import { featureTestIds as ids } from '../testIds';

// Stories and bugs rows on the CT phone project (390×844): key, sprint and state dot on the line under the title.

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-100' } };

const boxOf = async (locator: {
  boundingBox(): Promise<{ x: number; y: number; width: number; height: number } | null>;
}) => (await locator.boundingBox())!;

test('FeatureChildren_Rows_ShouldShowSprintAndStateDot', async ({ mount, page }) => {
  const [first] = feature.children;
  const item = {
    ...feature,
    children: [
      { ...first!, sprint: { id: 'sp24', name: 'Sprint 24', status: 'current' as const } },
      { ...first!, id: 'c-none', key: 'SHIP-142', sprint: null },
    ],
  };
  await stubApi(page, item);
  await stubSide(page, priya, [], []);
  await stubComments(page, priya, []);
  await stubFeature(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  const planned = screen.getByTestId(ids.childRow).first();
  const sprint = planned.getByTestId(ids.childSprint);
  await expect(sprint).toHaveText('Sprint 24');
  await expect(planned.getByTestId(ids.childState).locator('[data-category]')).toBeVisible();
  // Key, sprint and state share the line under the title, in that order.
  const key = await boxOf(planned.getByTestId(ids.childKey));
  const sprintBox = await boxOf(sprint);
  const state = await boxOf(planned.getByTestId(ids.childState));
  expect(sprintBox.y).toBe(key.y);
  expect(sprintBox.x).toBeGreaterThan(key.x + key.width - 1);
  expect(state.x).toBeGreaterThan(sprintBox.x + sprintBox.width - 1);
  // A story in no sprint shows Backlog, as the phone design does.
  await expect(screen.getByTestId(ids.childRow).nth(1).getByTestId(ids.childSprint)).toHaveText(
    'Backlog',
  );
});
