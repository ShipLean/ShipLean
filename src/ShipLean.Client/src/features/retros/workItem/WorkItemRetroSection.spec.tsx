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

const props = { projectId: 'p1', projectKey: 'SHIP', itemKey: 'SHIP-141', workItemId: 'w1' };

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } };

test('WorkItemRetroSection_WhenItemHasNoRetros_ShouldBeHidden', async ({ mount, page }) => {
  // Loading and empty both render nothing, so an empty item is mounted beside one with retros: once that one shows its
  // cards, both answers are in, and the empty item must still have no section.
  await stubRetros(page);
  await page.route('**/api/work-items/w-empty/retros', (r) =>
    r.fulfill({ status: 200, contentType: 'application/json', body: '{"cards":[],"rows":[]}' }),
  );
  await mount(
    <div>
      <WorkItemRetroSection {...props} workItemId="w-empty" />
      <WorkItemRetroSection {...props} />
    </div>,
    route,
  );
  await expect(page.getByTestId(ids.workItemRetroRow).first()).toBeVisible();
  await expect(page.getByTestId(ids.workItemRetroSection)).toHaveCount(1);
});

test('WorkItemRetroSection_WhenItemHasRetros_ShouldShowOneRowPerRetroWithCountsInOrder', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<WorkItemRetroSection {...props} />, route);
  const section = page.getByTestId(ids.workItemRetroSection);
  await expect(section.getByRole('heading', { name: 'Retrospectives' })).toBeVisible();
  const rows = section.getByTestId(ids.workItemRetroRow);
  await expect(rows).toHaveCount(workItemRetros.rows.length);
  await expect(section.getByTestId(shared.retroCard)).toHaveCount(0);
  const titles = section.getByRole('columnheader');
  await expect(titles).toHaveText([
    'Retro',
    'Logged',
    'Went well',
    'Not in the plan',
    'Went badly',
    'Can be improved',
    'To decide',
    'Decided',
  ]);
  // The first fixture row: one of each, 2 to decide, 1 of 3 decided.
  await expect(rows.first().getByRole('cell')).toHaveText([
    /Priya.*Claude Code/,
    '28 Oct',
    '1',
    '1',
    '1',
    '1',
    '2',
    '1/3',
  ]);
});

test('WorkItemRetroSection_WhenARowIsOpened_ShouldLinkWithRetro', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<WorkItemRetroSection {...props} />, route);
  const rows = page.getByTestId(ids.workItemRetroRow);
  for (const [index, row] of workItemRetros.rows.entries()) {
    await expect(rows.nth(index).getByRole('link')).toHaveAttribute(
      'href',
      `/p/SHIP/retros/items/SHIP-141?retro=${row.retroId}`,
    );
  }
});

test('WorkItemRetroSection_WhenShown_ShouldBeReadOnly', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<WorkItemRetroSection {...props} />, route);
  const section = page.getByTestId(ids.workItemRetroSection);
  await expect(section.getByTestId(ids.workItemRetroRow).first()).toBeVisible();
  for (const id of [
    ids.retroAddPoint,
    ids.retroPointMoveTheme,
    ids.retroPointAddAction,
    ids.retroPointArchive,
  ]) {
    await expect(section.getByTestId(id)).toHaveCount(0);
  }
  await expect(section.getByRole('button')).toHaveCount(0);
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
  await expect(section.getByTestId(ids.workItemRetroRow)).toHaveCount(workItemRetros.rows.length);
});

for (const theme of ['light', 'dark'] as const) {
  test(`WorkItemRetroSection_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubRetros(page);
    await mount(<WorkItemRetroSection {...props} />, route);
    await expect(page.getByTestId(ids.workItemRetroRow).first()).toBeVisible();
    await expect(page.getByTestId(ids.workItemRetroSection)).toHaveScreenshot(
      `work-item-retro-section.${theme}.png`,
      { animations: 'disabled' },
    );
  });
}
