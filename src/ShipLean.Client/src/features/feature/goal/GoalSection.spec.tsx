import { expect, test } from '@playwright/experimental-ct-react';
import { priya } from '../../workItem/ct/fixtures';
import { stubApi } from '../../workItem/ct/stubApi';
import { stubComments } from '../../workItem/ct/stubDetails';
import { stubSide } from '../../workItem/ct/stubSide';
import { WorkItemFormHarness } from '../../workItem/ct/WorkItemFormHarness';
import { workItemTestIds } from '../../workItem/testIds';
import { feature, stubFeature } from '../ct/stubFeature';
import { featureTestIds as ids } from '../testIds';

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-100' } };

type StubbedPage = Parameters<typeof stubApi>[0] &
  Parameters<typeof stubSide>[0] &
  Parameters<typeof stubComments>[0] &
  Parameters<typeof stubFeature>[0];

async function stubAll(page: StubbedPage, respondToPatch?: Parameters<typeof stubApi>[2]) {
  await stubApi(page, feature, respondToPatch);
  await stubSide(page, priya, [], []);
  await stubComments(page, priya, []);
  await stubFeature(page);
}

test('GoalSection_WhenEdited_ShouldMarkDirty', async ({ mount, page }) => {
  await stubAll(page);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);
  await expect(screen.getByTestId(workItemTestIds.save)).toBeDisabled();

  await screen.getByTestId(ids.goalEdit).click();
  await screen.getByTestId(ids.goalInput).fill('Two taps to pay.');

  await expect(screen.getByTestId(workItemTestIds.save)).toBeEnabled();
  await expect(
    screen.getByTestId(ids.goal).getByRole('img', { name: 'Unsaved change' }),
  ).toBeVisible();
});

test('GoalSection_WhenOver1000Characters_ShouldShowFieldError', async ({ mount, page }) => {
  await stubAll(page, () => ({
    status: 400,
    body: { error: 'A goal is at most 1,000 characters.', code: 'validation', field: 'goal' },
  }));
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  await screen.getByTestId(ids.goalEdit).click();
  await screen.getByTestId(ids.goalInput).fill('x'.repeat(1001));
  await screen.getByTestId(workItemTestIds.save).click();

  await expect(screen.getByTestId(ids.goalError)).toHaveText('A goal is at most 1,000 characters.');
});
