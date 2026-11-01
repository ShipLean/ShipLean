import { expect, test } from '@playwright/experimental-ct-react';
import { ToastHost } from '../../../components/ToastHost';
import { retroInsights, stubRetros } from '../ct/retroFixtures';
import { retrosTestIds as ids } from '../testIds';
import { insightsProject } from './ct/insightsProject';
import { ActionItemsTable } from './ActionItemsTable';
import { RetroInsightsPanel } from './RetroInsightsPanel';

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

const open = { hooksConfig: { route: '/p/SHIP/retros/insights?view=actions' } };

const sprintsOf = [
  { id: 'sprint-24', name: 'Sprint 24', status: 'current', startDate: '2026-10-05' },
  { id: 'sprint-26', name: 'Sprint 26', status: 'future', startDate: '2026-11-16' },
  { id: 'sprint-25', name: 'Sprint 25', status: 'future', startDate: '2026-11-02' },
];

interface Write {
  method: string;
  path: string;
  body: unknown;
}

async function stubWorkItems(page: Page, patchStatus = 200) {
  const writes: Write[] = [];
  await page.route(
    (url) => url.pathname === `/api/projects/${insightsProject.id}/sprints`,
    (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(sprintsOf),
      }),
  );
  await page.route(
    (url) => /\/work-items\/by-key\/[^/]+$/.test(url.pathname),
    (route) => {
      const key = new URL(route.request().url()).pathname.split('/').pop();
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ id: `id-${key}`, key, revision: 7 }),
      });
    },
  );
  await page.route(
    (url) => /^\/api\/work-items\/[^/]+$/.test(url.pathname),
    (route) => {
      const request = route.request();
      writes.push({
        method: request.method(),
        path: new URL(request.url()).pathname,
        body: request.postDataJSON() as unknown,
      });
      return patchStatus === 200
        ? route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ id: 'x', key: 'x', revision: 8, notices: [] }),
          })
        : route.fulfill({
            status: patchStatus,
            contentType: 'application/json',
            body: JSON.stringify({ error: 'Could not move it', code: 'validation' }),
          });
    },
  );
  return writes;
}

test('ActionItemsTable_WhenMounted_ShouldListRowsInFixtureOrder', async ({ mount, page }) => {
  await stubRetros(page);
  await stubWorkItems(page);
  await mount(<ActionItemsTable project={insightsProject} insights={retroInsights} />, open);
  const rows = page.getByTestId(ids.retroActionRow);
  await expect(rows).toHaveCount(retroInsights.actionItems.length);
  const keys = await rows.evaluateAll((els) => els.map((e) => e.getAttribute('data-item-key')));
  expect(keys).toEqual(retroInsights.actionItems.map((r) => r.action.key));
  const first = retroInsights.actionItems[0];
  const row = rows.first();
  await expect(row.locator('[data-cell="theme"]')).toHaveText(first.theme!.name);
  await expect(row.locator('[data-cell="raised"]')).toHaveText(first.raisedIn!.name);
  await expect(row.locator('[data-cell="state"]')).toHaveText(first.action.state.name);
  await expect(row.locator('[data-cell="points"]')).toHaveText(String(first.action.points));
});

test('ActionItemsTable_WhenOwnerAndImpactAreNull_ShouldShowDashes', async ({ mount, page }) => {
  await stubRetros(page);
  await stubWorkItems(page);
  await mount(<ActionItemsTable project={insightsProject} insights={retroInsights} />, open);
  const row = page.getByTestId(ids.retroActionRow).first();
  await expect(row.locator('[data-cell="owner"]')).toHaveText('—');
  await expect(row.locator('[data-cell="impact"]')).toHaveText('—');
});

test('ActionItemsTable_WhenOwnerAndImpactSet_ShouldShowNameAndVerdict', async ({ mount, page }) => {
  await stubRetros(page);
  await stubWorkItems(page);
  await mount(<ActionItemsTable project={insightsProject} insights={retroInsights} />, open);
  const index = retroInsights.actionItems.findIndex((r) => r.owner && r.impact);
  const row = page.getByTestId(ids.retroActionRow).nth(index);
  await expect(row.locator('[data-cell="owner"]')).toContainText(
    retroInsights.actionItems[index].owner!.name,
  );
  await expect(row.locator('[data-cell="impact"] [data-verdict]')).toHaveAttribute(
    'data-verdict',
    retroInsights.actionItems[index].impact!,
  );
});

test('ActionItemsTable_WhenItemHasNoSprint_ShouldOfferTheNextSprint', async ({ mount, page }) => {
  await stubRetros(page);
  await stubWorkItems(page);
  await mount(<ActionItemsTable project={insightsProject} insights={retroInsights} />, open);
  const unplanned = retroInsights.actionItems.filter(
    (r) => r.action.sprint === null && r.action.state.category === 'proposed',
  );
  const pulls = page.getByTestId(ids.retroActionPull);
  await expect(pulls).toHaveCount(unplanned.length);
  await expect(pulls.first()).toHaveText('Pull into Sprint 25');
});

test('ActionItemsTable_WhenPulled_ShouldPatchTheSprintAndRefetch', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  const writes = await stubWorkItems(page);
  await mount(<RetroInsightsPanel project={insightsProject} />, open);
  const pull = page.getByTestId(ids.retroActionPull).first();
  const key = (await pull.getAttribute('data-item-key'))!;
  await expect.poll(() => stub.reads('/retros/insights').length).toBe(1);
  await pull.click();
  await expect.poll(() => writes.length).toBe(1);
  expect(writes[0].method).toBe('PATCH');
  expect(writes[0].path).toBe(`/api/work-items/id-${key}`);
  const body = writes[0].body as { expectedRevision: number; changes: { sprint: unknown } };
  expect(body.expectedRevision).toBe(7);
  expect(body.changes.sprint).toEqual({ value: 'sprint-25' });
  await expect.poll(() => stub.reads('/retros/insights').length).toBe(2);
});

test('ActionItemsTable_WhenPullFails_ShouldToastTheError', async ({ mount, page }) => {
  await stubRetros(page);
  await stubWorkItems(page, 400);
  await mount(
    <>
      <RetroInsightsPanel project={insightsProject} />
      <ToastHost />
    </>,
    open,
  );
  await page.getByTestId(ids.retroActionPull).first().click();
  await expect(page.getByText('Could not move it')).toBeVisible();
});

for (const theme of ['light', 'dark'] as const) {
  test(`ActionItemsTable_WhenRendered_ShouldMatchItsScreenshot_${theme}`, async ({
    mount,
    page,
  }) => {
    await stubRetros(page);
    await stubWorkItems(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const view = await mount(
      <ActionItemsTable project={insightsProject} insights={retroInsights} />,
      open,
    );
    await expect(page.getByTestId(ids.retroActionPull).first()).toBeVisible();
    await expect(view).toHaveScreenshot(`action-items.${theme}.png`, { animations: 'disabled' });
  });
}
