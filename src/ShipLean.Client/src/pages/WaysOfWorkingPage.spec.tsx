import { expect, test } from '@playwright/experimental-ct-react';
import { pagesTestIds } from '../features/pages/testIds';
import { shellTestIds } from '../features/shell/testIds';
import { RouteHistoryHarness } from './ct/RouteHistoryHarness';
import { routeHistoryIds } from './ct/routeHistoryIds';
import { stubProjectApi } from './pagesFixtures';

// SHIP-2280 D9: the old Prompts tab's address (docs and agent text link to it) goes to Ways of working. The redirect
// replaces, so Back leaves instead of landing on the redirect again.

test('AppRoutes_WhenOldPromptsSettingsUrl_ShouldRedirectKeepingPrompt', async ({ mount, page }) => {
  await stubProjectApi(page);
  await mount(<RouteHistoryHarness to="/p/SHIP/settings/prompts?prompt=issueProposal" />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories' },
  });
  const location = page.getByTestId(routeHistoryIds.location);
  await expect(location).toHaveText('/p/SHIP/backlogs/stories');

  await page.getByTestId(routeHistoryIds.go).click();
  await expect(location).toHaveText('/p/SHIP/ways-of-working?prompt=issueProposal');
  await expect(page.getByTestId(pagesTestIds.waysOfWorkingPage)).toBeVisible();
  await expect(page.getByTestId(pagesTestIds.projectSettingsPage)).toHaveCount(0);
  await expect(page.getByTestId(shellTestIds.navWaysOfWorking)).toHaveAttribute(
    'aria-current',
    'page',
  );

  await page.getByTestId(routeHistoryIds.back).click();
  await expect(location).toHaveText('/p/SHIP/backlogs/stories');
});

// With no prompt in the old address the new one carries none either, and the page opens its first prompt as it does on
// any visit without ?prompt=.
test('AppRoutes_WhenOldUrlWithoutPrompt_ShouldRedirectToTheFirstPrompt', async ({
  mount,
  page,
}) => {
  await stubProjectApi(page);
  await mount(<RouteHistoryHarness to="/" />, {
    hooksConfig: { route: '/p/SHIP/settings/prompts' },
  });
  const location = page.getByTestId(routeHistoryIds.location);
  await expect(location).toHaveText('/p/SHIP/ways-of-working');
  await expect(page.getByTestId(pagesTestIds.waysOfWorkingPage)).toBeVisible();
});
