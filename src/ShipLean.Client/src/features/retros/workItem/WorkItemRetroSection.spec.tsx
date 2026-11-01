import { expect, test } from '@playwright/experimental-ct-react';
import { retroSharedTestIds as shared } from '../../retroShared/testIds';
import { feature, stubFeature } from '../../feature/ct/stubFeature';
import { priya } from '../../workItem/ct/fixtures';
import { stubApi } from '../../workItem/ct/stubApi';
import { stubComments } from '../../workItem/ct/stubDetails';
import { stubSide } from '../../workItem/ct/stubSide';
import { stubRetros, workItemRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { WorkItemRetroHarness } from './ct/WorkItemRetroHarness';
import { WorkItemRetroSection } from './WorkItemRetroSection';

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } };

test('WorkItemRetroSection_WhenItemHasNoRetros_ShouldBeHidden', async ({ mount, page }) => {
  await stubRetros(page);
  await page.route('**/api/work-items/*/retros', (r) =>
    r.fulfill({ status: 200, contentType: 'application/json', body: '{"cards":[]}' }),
  );
  await mount(<WorkItemRetroSection projectId="p1" workItemId="w1" />, route);
  await expect(page.getByTestId(ids.workItemRetroSection)).toHaveCount(0);
});

test('WorkItemRetroSection_WhenItemHasRetros_ShouldShowCardsWithTheFailureLogCollapsed', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<WorkItemRetroSection projectId="p1" workItemId="w1" />, route);
  const section = page.getByTestId(ids.workItemRetroSection);
  await expect(section.getByRole('heading', { name: 'Retrospective' })).toBeVisible();
  await expect(section.getByTestId(shared.retroCard)).toHaveCount(workItemRetros.cards.length);
  await expect(section.getByTestId(shared.retroCardLog)).toHaveCount(0);
  await expect(section.getByTestId(shared.retroCardLogToggle).first()).toHaveAttribute(
    'aria-expanded',
    'false',
  );
});

test('WorkItemRetroSection_WhenShown_ShouldBeReadOnly', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<WorkItemRetroSection projectId="p1" workItemId="w1" />, route);
  const section = page.getByTestId(ids.workItemRetroSection);
  await expect(section.getByTestId(shared.retroCard).first()).toBeVisible();
  for (const id of [
    ids.retroAddPoint,
    ids.retroPointMoveTheme,
    ids.retroPointAddAction,
    ids.retroPointArchive,
  ]) {
    await expect(section.getByTestId(id)).toHaveCount(0);
  }
  await expect(section.getByRole('button').filter({ hasNotText: 'Raw failure log' })).toHaveCount(
    0,
  );
});

test('WorkItemRetroSection_WhenOnAFeatureForm_ShouldShowTheConductorRetro', async ({
  mount,
  page,
}) => {
  await stubApi(page, feature);
  await stubSide(page, priya, [], []);
  await stubComments(page, priya, []);
  await stubFeature(page);
  await stubRetros(page);
  await mount(<WorkItemRetroHarness itemKey={feature.key} />, {
    hooksConfig: { route: `/p/SHIP/items/${feature.key}` },
  });
  const section = page.getByTestId(ids.workItemRetroSection);
  await expect(section).toBeVisible();
  await expect(section.getByTestId(shared.retroCard)).toHaveCount(workItemRetros.cards.length);
});

for (const theme of ['light', 'dark'] as const) {
  test(`WorkItemRetroSection_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubRetros(page);
    await mount(<WorkItemRetroSection projectId="p1" workItemId="w1" />, route);
    await expect(page.getByTestId(shared.retroCard).first()).toBeVisible();
    await expect(page.getByTestId(ids.workItemRetroSection)).toHaveScreenshot(
      `work-item-retro-section.${theme}.png`,
      { animations: 'disabled' },
    );
  });
}
