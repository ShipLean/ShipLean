import { expect, test } from '@playwright/experimental-ct-react';
import { stubShell } from '../../../pages/screenshots/shellStub';
import { inspectionMetricsTestIds } from '../../inspectionMetrics/testIds';
import { ShellHarness } from '../ct/ShellHarness';
import { shellTestIds as ids } from '../testIds';

// SHIP-2280 D2, D10: Ways of working is a side-menu section of its own, under Inspection and above Jira import.
test('SideMenu_WhenProject_ShouldShowWaysOfWorkingBetweenInspectionAndJiraImport', async ({
  mount,
  page,
}) => {
  await stubShell(page);
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/ways-of-working' } });

  const menu = page.getByTestId(ids.sideMenu);
  const item = menu.getByTestId(ids.navWaysOfWorking);
  await expect(item).toHaveText('✎Ways of working');
  await expect(item).toHaveAttribute('href', '/p/SHIP/ways-of-working');
  await expect(item).toHaveAttribute('aria-current', 'page');
  await expect(menu.getByTestId(inspectionMetricsTestIds.menuItem)).not.toHaveAttribute(
    'aria-current',
  );

  const sections = menu.locator(
    [inspectionMetricsTestIds.menuItem, ids.navWaysOfWorking, ids.navJiraImport, ids.navSettings]
      .map((id) => `[data-testid="${id}"]`)
      .join(','),
  );
  await expect(sections).toHaveCount(4);
  const order = await sections.evaluateAll((els) =>
    els.map((el) => el.getAttribute('data-testid')),
  );
  expect(order).toEqual([
    inspectionMetricsTestIds.menuItem,
    ids.navWaysOfWorking,
    ids.navJiraImport,
    ids.navSettings,
  ]);
});

test('SideMenu_WhenOnAnotherSection_ShouldNotLightWaysOfWorking', async ({ mount, page }) => {
  await stubShell(page);
  await mount(<ShellHarness />, { hooksConfig: { route: '/p/SHIP/import' } });

  await expect(page.getByTestId(ids.navJiraImport)).toHaveAttribute('aria-current', 'page');
  await expect(page.getByTestId(ids.navWaysOfWorking)).toBeVisible();
  await expect(page.getByTestId(ids.navWaysOfWorking)).not.toHaveAttribute('aria-current');
});
