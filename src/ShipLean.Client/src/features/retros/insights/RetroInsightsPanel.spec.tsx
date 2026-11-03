import { expect, test } from '@playwright/experimental-ct-react';
import { InsightsAddressHarness } from '../ct/InsightsAddressHarness';
import { stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { insightsProject } from './ct/insightsProject';

test('RetroInsightsPanel_WhenRangeChosen_ShouldWriteItToTheAddressAndAskForIt', async ({
  mount,
  page,
}) => {
  const stub = await stubRetros(page);
  await mount(<InsightsAddressHarness project={insightsProject} />, {
    hooksConfig: { route: '/p/SHIP/retros/insights' },
  });
  const range = page.getByTestId(ids.retroInsightsRange);
  await expect(range.locator('option')).toHaveText([
    'Last 6 sprints',
    'Last 3 sprints',
    'All sprints',
    'Last 6 weeks',
    'Last 12 weeks',
  ]);
  await range.selectOption('w12');
  await expect(page.getByTestId('test-address')).toHaveText('?range=w12');
  await expect
    .poll(() => stub.reads('/retros/insights').map((c) => c.query))
    .toContain('range=w12');
  await expect(range).toHaveValue('w12');
  await expect(page.getByTestId(ids.retroInsightsNosprint)).toHaveCount(0);
});
