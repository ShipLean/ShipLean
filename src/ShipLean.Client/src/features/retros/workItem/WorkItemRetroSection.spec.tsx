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
import { WorkItemRetroLocationHarness } from './ct/WorkItemRetroLocationHarness';
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

test('WorkItemRetroSection_WhenARowIsClickedOutsideItsLink_ShouldOpenTheSameFilteredRetro', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<WorkItemRetroLocationHarness {...props} />, route);
  const rows = page.getByTestId(ids.workItemRetroRow);
  await expect(rows.first()).toBeVisible();
  const second = workItemRetros.rows[1];
  await rows.nth(1).getByRole('cell').nth(1).click();
  await expect(page.getByTestId('location-probe')).toHaveText(
    `/p/SHIP/retros/items/SHIP-141?retro=${second.retroId}`,
  );
});

test('WorkItemRetroSection_WhenARowHasFocus_ShouldOpenOnlyThroughItsLink', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<WorkItemRetroLocationHarness {...props} />, route);
  const first = page.getByTestId(ids.workItemRetroRow).first();
  await first.getByRole('link').focus();
  await page.keyboard.press('Enter');
  await expect(page.getByTestId('location-probe')).toHaveText(
    `/p/SHIP/retros/items/SHIP-141?retro=${workItemRetros.rows[0].retroId}`,
  );
  await expect(first.getByRole('link')).toHaveCount(1);
});

test('WorkItemRetroSection_WhenTheColumnIs664Wide_ShouldShowEveryCountAndDecidedWithoutScrolling', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  const long = structuredClone(workItemRetros);
  long.rows[0].author.agentName = 'Claude Code with a very long agent name indeed';
  long.rows[0].author.user.name = 'Bartholomew Longname-Fitzgerald';
  await page.route('**/api/work-items/w1/retros', (r) =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(long) }),
  );
  await mount(
    <div style={{ width: 664 }}>
      <WorkItemRetroSection {...props} />
    </div>,
    route,
  );
  const section = page.getByTestId(ids.workItemRetroSection);
  await expect(section.getByTestId(ids.workItemRetroRow).first()).toBeVisible();
  const box = await section.locator('table').evaluate((table) => {
    const holder = table.parentElement as HTMLElement;
    const right = holder.getBoundingClientRect().right;
    const cells = [...table.querySelectorAll('tbody tr:first-child td')];
    const last = cells[cells.length - 1].getBoundingClientRect().right;
    return { scroll: holder.scrollWidth, client: holder.clientWidth, right, last };
  });
  expect(box.scroll).toBeLessThanOrEqual(box.client);
  expect(box.last).toBeLessThanOrEqual(box.right);
  for (const name of ['Went well', 'Not in the plan', 'Went badly', 'Can be improved', 'Decided']) {
    await expect(section.getByRole('columnheader', { name })).toBeVisible();
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
