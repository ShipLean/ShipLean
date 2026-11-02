import { expect, test } from '@playwright/experimental-ct-react';
import { PROJECT, planningView } from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { backlogTestIds as ids } from '../testIds';
import { ToastHost } from '../../../components/ToastHost';
import { componentTestIds } from '../../../components/testIds';
import { PlanningMode } from './PlanningMode';

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' } };
const toggleOf = (page: import('playwright').Page, title: string) =>
  page.getByTestId(ids.planningSection).filter({ hasText: title }).getByTestId(ids.sectionToggle);
const rowOf = (page: import('playwright').Page, key: string) =>
  page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));

// `saved: {}` is a user who has never collapsed anything: the app's default applies.
test('Running_sprint_starts_closed', async ({ mount, page }) => {
  await stubBacklog(page, planningView(), { saved: {} });
  await mount(<PlanningMode project={PROJECT} />, route);

  await expect(page.getByTestId(ids.planningSectionTitle)).toHaveCount(4);
  await expect(toggleOf(page, 'Sprint 24')).toHaveAttribute('aria-expanded', 'false');
  await expect(toggleOf(page, 'Sprint 25')).toHaveAttribute('aria-expanded', 'true');
  await expect(rowOf(page, 'SHIP-160')).toHaveCount(0);
  await expect(rowOf(page, 'SHIP-158')).toBeVisible();
  // A closed section still shows its sum.
  await expect(
    page.getByTestId(ids.planningSection).first().getByTestId(ids.planningCommitted),
  ).toHaveText('26 / 23 pts');
});

test('Opening_it_is_remembered', async ({ mount, page }) => {
  const server = await stubBacklog(page, planningView(), { saved: {} });
  await mount(<PlanningMode project={PROJECT} />, route);

  await toggleOf(page, 'Sprint 24').click();
  await expect(rowOf(page, 'SHIP-160')).toBeVisible();
  await expect.poll(() => server.puts).toEqual([{ collapsedSections: [] }]);
});

test('Opening_it_is_remembered_on_the_next_visit', async ({ mount, page }) => {
  await stubBacklog(page, planningView(), { saved: { collapsedSections: [] } });
  await mount(<PlanningMode project={PROJECT} />, route);

  await expect(rowOf(page, 'SHIP-160')).toBeVisible();
  await expect(toggleOf(page, 'Sprint 24')).toHaveAttribute('aria-expanded', 'true');
});

test('Collapse_is_saved_in_the_preference', async ({ mount, page }) => {
  const server = await stubBacklog(page, planningView(), { saved: { forecast: true } });
  await mount(<PlanningMode project={PROJECT} />, route);

  // Closing Sprint 25 saves every closed section, so the running sprint stays closed.
  await toggleOf(page, 'Sprint 25').click();
  await expect(rowOf(page, 'SHIP-158')).toHaveCount(0);
  await expect
    .poll(() => server.puts)
    .toEqual([{ forecast: true, collapsedSections: ['p1:sp24', 'p1:sp25'] }]);

  await toggleOf(page, 'Backlog').click();
  await expect(rowOf(page, 'SHIP-170')).toHaveCount(0);
  await expect
    .poll(() => server.puts.at(-1))
    .toEqual({
      forecast: true,
      collapsedSections: ['p1:sp24', 'p1:sp25', 'p1:backlog'],
    });
});

test('Selected_rows_stay_selected_when_collapsed', async ({ mount, page }) => {
  await stubBacklog(page, planningView(), { saved: { collapsedSections: [] } });
  await mount(<PlanningMode project={PROJECT} />, route);

  await rowOf(page, 'SHIP-158').getByTestId(ids.rowSelect).getByRole('checkbox').click();
  await rowOf(page, 'SHIP-170').getByTestId(ids.rowSelect).getByRole('checkbox').click();
  await expect(page.getByTestId(ids.bulkCount)).toHaveText('2 selected');

  await toggleOf(page, 'Sprint 25').click();
  await expect(rowOf(page, 'SHIP-158')).toHaveCount(0);
  await expect(page.getByTestId(ids.bulkCount)).toHaveText('2 selected');

  await toggleOf(page, 'Sprint 25').click();
  await expect(rowOf(page, 'SHIP-158').getByRole('checkbox').first()).toBeChecked();
});

test('Search_opens_sections_with_hits_without_saving', async ({ mount, page }) => {
  const server = await stubBacklog(page, planningView(), { saved: {} });
  await mount(<PlanningMode project={PROJECT} />, route);
  await expect(rowOf(page, 'SHIP-160')).toHaveCount(0);

  await page.getByTestId(ids.searchBox).fill('SHIP-160');
  await expect(rowOf(page, 'SHIP-160')).toBeVisible();
  await expect(toggleOf(page, 'Sprint 24')).toHaveAttribute('aria-expanded', 'true');
  await expect(toggleOf(page, 'Sprint 24')).toBeDisabled();

  await page.getByTestId(ids.searchBox).fill('');
  await expect(rowOf(page, 'SHIP-160')).toHaveCount(0);
  expect(server.puts).toHaveLength(0);
});

test('Collapse_is_per_project_and_keeps_other_projects_entries', async ({ mount, page }) => {
  // Another project closed its Backlog and Sprint 24; this project has not collapsed anything.
  const server = await stubBacklog(page, planningView(), {
    saved: { collapsedSections: ['p2:backlog', 'p2:sp24'] },
  });
  await mount(<PlanningMode project={PROJECT} />, route);

  await expect(toggleOf(page, 'Backlog')).toHaveAttribute('aria-expanded', 'true');
  await expect(rowOf(page, 'SHIP-170')).toBeVisible();

  await toggleOf(page, 'Sprint 25').click();
  await expect
    .poll(() => server.puts)
    .toEqual([{ collapsedSections: ['p2:backlog', 'p2:sp24', 'p1:sp25'] }]);
});

test('A_failed_save_shows_the_reason_in_a_toast', async ({ mount, page }) => {
  await stubBacklog(page, planningView(), { saved: {} });
  await page.route('**/api/me/preferences/**', (r) =>
    r.request().method() === 'PUT'
      ? r.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Preferences are down', code: 'validation' }),
        })
      : r.fallback(),
  );
  await mount(
    <>
      <ToastHost />
      <PlanningMode project={PROJECT} />
    </>,
    route,
  );

  await toggleOf(page, 'Sprint 24').click();
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('Preferences are down');
});
