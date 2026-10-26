import { expect, test } from '@playwright/experimental-ct-react';
import { mei, priya } from '../../workItem/ct/fixtures';
import { stubApi } from '../../workItem/ct/stubApi';
import { comment, stubComments } from '../../workItem/ct/stubDetails';
import { stubSide } from '../../workItem/ct/stubSide';
import { WorkItemFormHarness } from '../../workItem/ct/WorkItemFormHarness';
import { workItemTestIds } from '../../workItem/testIds';
import { feature, stubFeature } from '../ct/stubFeature';
import { featureTestIds as ids } from '../testIds';

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-100' } };
const older = comment('c1', priya, 'First entry');
const newer = comment('c2', mei, 'Second entry');
const byAgent = { ...comment('c3', mei, 'Agent entry'), agentName: 'Claude Code (claude.ai)' };

// The page as each stub sees it.
type StubbedPage = Parameters<typeof stubApi>[0] &
  Parameters<typeof stubSide>[0] &
  Parameters<typeof stubComments>[0] &
  Parameters<typeof stubFeature>[0];

async function stubAll(page: StubbedPage, entries: (typeof older)[]) {
  await stubApi(page, feature);
  await stubSide(page, priya, [], []);
  await stubFeature(page);
  return stubComments(page, priya, entries);
}

test('FeatureLog_ShouldShowNewestFirst', async ({ mount, page }) => {
  await stubAll(page, [older, newer]);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  const log = screen.getByTestId(ids.log);
  await expect(log.getByRole('heading', { name: 'Log' })).toBeVisible();
  await expect(log.getByTestId(ids.logCount)).toHaveText('2');
  const rows = log.getByTestId(workItemTestIds.comment);
  await expect(rows.nth(0).getByTestId(workItemTestIds.commentBody)).toHaveText('Second entry');
  await expect(rows.nth(1).getByTestId(workItemTestIds.commentBody)).toHaveText('First entry');
});

test('FeatureLog_WhenNoEntries_ShouldSayNoLogEntries', async ({ mount, page }) => {
  await stubAll(page, []);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  await expect(screen.getByTestId(ids.logEmpty)).toHaveText('No log entries yet.');
});

test('FeatureLog_WhenAgentEntry_ShouldShowViaMcp', async ({ mount, page }) => {
  await stubAll(page, [older, byAgent]);
  const screen = await mount(<WorkItemFormHarness itemKey="SHIP-100" />, route);

  const rows = screen.getByTestId(ids.log).getByTestId(workItemTestIds.comment);
  await expect(rows.nth(0).getByTestId(workItemTestIds.commentVia)).toHaveText('Agent · via MCP');
  await expect(rows.nth(1).getByTestId(workItemTestIds.commentVia)).toHaveCount(0);
});
