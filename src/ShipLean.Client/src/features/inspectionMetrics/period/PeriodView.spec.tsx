import { expect, test } from '@playwright/experimental-ct-react';
import type { InspectionMetricsViewDto } from '@shiplean/core/api/views';
import { AppRoutes } from '../../../AppRoutes';
import { stubProjectApi } from '../../../pages/pagesFixtures';
import {
  routeInspectionMetrics,
  stubFeaturesTab,
  stubPeriod,
  stubView,
} from '../ct/stubInspectionMetrics';
import { inspectionMetricsTestIds as ids } from '../testIds';

const VIEW = stubPeriod({ endedBy: 'lastCommit' });
const DETAIL = VIEW.periodDetail!;
const SELECTED = DETAIL.selected!.task;
const ROUTE = `/p/SHIP/inspection?view=period&p=${DETAIL.periodId}&select=${SELECTED.key}`;

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];
type Mount = Parameters<Parameters<typeof test>[2]>[0]['mount'];

async function open(
  mount: Mount,
  page: Page,
  route = ROUTE,
  view: InspectionMetricsViewDto = VIEW,
) {
  await page.setViewportSize({ width: 1440, height: 900 });
  await stubProjectApi(page);
  const server = await routeInspectionMetrics(page, view);
  await mount(<AppRoutes />, { hooksConfig: { route } });
  await expect(page.getByTestId(ids.period)).toBeVisible();
  return server;
}

for (const theme of ['light', 'dark'] as const) {
  test(`PeriodView_WhenTasksArrive_ShouldKeepTheServersOrder_${theme}`, async ({ mount, page }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    // The fixture's tasks are slowest first, which is not key order (SHIP-194 before SHIP-192).
    const keys = DETAIL.tasks.map((t) => t.key);
    expect(keys).not.toEqual([...keys].sort());
    await open(mount, page);
    const rows = page.getByTestId(ids.taskRow);
    await expect(rows).toHaveCount(keys.length);
    const shown = await rows.evaluateAll((els) => els.map((el) => el.getAttribute('data-key')));
    expect(shown).toEqual(keys);
    await expect(rows.and(page.locator(`[data-key="${SELECTED.key}"]`))).toHaveAttribute(
      'aria-selected',
      'true',
    );
  });

  test(`PeriodView_WhenTheTaskEndedAtResolved_ShouldSayResolvedInTheRail_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await open(mount, page, ROUTE, stubPeriod({ endedBy: 'resolved' }));
    const end = page.getByTestId(ids.railEvent).and(page.locator('[data-kind="end"]'));
    await expect(end).toContainText('Lead time ends at Resolved');
    await expect(end).not.toContainText('last commit');
    await expect(page.getByTestId(ids.taskRowEndedBy)).toHaveCount(DETAIL.tasks.length);
  });

  test(`PeriodView_WhenTheTaskEndedAtItsLastCommit_ShouldLabelTheTailNotCounted_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await open(mount, page);
    const end = page.getByTestId(ids.railEvent).and(page.locator('[data-kind="end"]'));
    await expect(end).toContainText('Lead time ends at the last commit');
    const tail = DETAIL.selected!.segments.find((s) => s.kind === 'unresolvedTail')!;
    await expect(page.getByTestId(ids.railNotCounted)).toHaveText(
      'Commit to Resolved, not counted',
    );
    await expect(
      page.getByTestId(ids.railSegment).and(page.locator('[data-kind="unresolvedTail"]')),
    ).toHaveAttribute('data-counted', 'false');
    expect(tail.counted).toBe(false);
    // Events after the end (the approval and the final Resolved) are not counted; the rest are.
    const ended = Date.parse(SELECTED.endedAt);
    const after = DETAIL.selected!.events.filter((e) => Date.parse(e.at) > ended).length;
    expect(after).toBeGreaterThan(0);
    await expect(
      page.getByTestId(ids.railEvent).and(page.locator('[data-counted="false"]')),
    ).toHaveCount(after);
    await expect(page.getByTestId(ids.taskRowEndedBy)).toHaveCount(0);
  });

  test(`PeriodView_WhenATaskIsSelected_ShouldLinkItsFeatureTimelineWithTheTask_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await open(mount, page);
    const link = page.getByTestId(ids.railTimelineLink);
    await expect(link).toHaveAttribute(
      'href',
      `/p/SHIP/items/${SELECTED.featureKey}/timeline?select=${SELECTED.key}`,
    );
    await expect(page.getByTestId(ids.railOpenItem)).toHaveAttribute(
      'href',
      `/p/SHIP/items/${SELECTED.key}`,
    );
  });

  test(`PeriodView_WhenTheFeaturesTabIsOpen_ShouldRenderAFeatureRowEach_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    const view = stubFeaturesTab();
    const features = view.periodDetail!.features;
    await open(
      mount,
      page,
      `/p/SHIP/inspection?view=period&p=${DETAIL.periodId}&tab=features`,
      view,
    );
    const rows = page.getByTestId(ids.featureRow);
    await expect(rows).toHaveCount(features.length);
    for (const feature of features) {
      const row = rows.and(page.locator(`[data-key="${feature.key}"]`));
      await expect(row).toContainText(feature.title);
      await expect(row).toContainText(
        `${feature.tasksEnded} of ${feature.tasksTotal} · ${feature.tasksEndedBefore} before`,
      );
      await expect(row).toContainText(feature.executionLabel!);
    }
    await expect(page.getByTestId(ids.taskRows)).toHaveCount(0);
    await expect(page.getByTestId(ids.periodTabFeatures)).toHaveAttribute('aria-current', 'page');
  });
}

test('PeriodView_WhenAnotherTaskIsSelected_ShouldReadItAndPostPeriodOpenedOnce', async ({
  mount,
  page,
}) => {
  const server = await open(mount, page);
  const other = DETAIL.tasks.find((t) => t.key !== SELECTED.key)!;
  await page
    .getByTestId(ids.taskRow)
    .and(page.locator(`[data-key="${other.key}"]`))
    .click();
  await expect.poll(() => server.reads.at(-1)).toContain(`select=${other.key}`);
  await page.getByTestId(ids.periodTabFeatures).click();
  await expect.poll(() => server.reads.length).toBe(2);
  await expect(page.getByTestId(ids.featureRows)).toBeVisible();
  expect(server.usage.filter((u) => u.path === 'inspection-metrics-period-opened')).toEqual([
    { path: 'inspection-metrics-period-opened', body: { period: 'finished' } },
  ]);
});

test('PeriodView_WhenTheTaskIsOpenedFromTheRail_ShouldPostTaskOpenedFromRail', async ({
  mount,
  page,
}) => {
  const server = await open(mount, page);
  await page.getByTestId(ids.railOpenItem).click();
  await expect
    .poll(() => server.usage.filter((u) => u.path === 'inspection-metrics-task-opened'))
    .toEqual([{ path: 'inspection-metrics-task-opened', body: { from: 'rail' } }]);
});

test('PeriodView_WhenNoSelectAndServerSelectsTheSlowest_ShouldDrawItInTheRail', async ({
  mount,
  page,
}) => {
  // The conductor's ruling (D1): with a sprint and no select, the server selects the slowest task.
  const slowest = DETAIL.tasks[0]!;
  const view = stubView({
    periodDetail: { ...DETAIL, selected: { ...DETAIL.selected!, task: slowest } },
  });
  const server = await open(
    mount,
    page,
    `/p/SHIP/inspection?view=period&p=${DETAIL.periodId}`,
    view,
  );
  await expect(page.getByTestId(ids.rail)).toHaveAttribute('data-key', slowest.key);
  await expect(
    page.getByTestId(ids.taskRow).and(page.locator(`[data-key="${slowest.key}"]`)),
  ).toHaveAttribute('aria-selected', 'true');
  expect(server.reads).toHaveLength(1);
});

test('PeriodView_WhenTheSelectIsUnknown_ShouldShowTheEmptyRail', async ({ mount, page }) => {
  await open(
    mount,
    page,
    `/p/SHIP/inspection?view=period&p=${DETAIL.periodId}&select=SHIP-0`,
    stubView({ periodDetail: { ...DETAIL, selected: null } }),
  );
  await expect(page.getByTestId(ids.railEmpty)).toBeVisible();
  await expect(page.getByTestId(ids.rail)).toHaveCount(0);
});

for (const theme of ['light', 'dark'] as const) {
  for (const inProgress of [false, true]) {
    test(`PeriodView_WhenNoTaskEnded_InProgress_${inProgress}_ShouldSaySoInBothTabsAndTheRail_${theme}`, async ({
      mount,
      page,
    }) => {
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      const text = inProgress
        ? 'No tasks ended in this sprint yet'
        : 'No tasks ended in this sprint';
      const empty = stubView({
        periodDetail: { ...DETAIL, inProgress, tasks: [], features: [], selected: null },
      });
      const route = `/p/SHIP/inspection?view=period&p=${DETAIL.periodId}`;
      await open(mount, page, route, empty);
      await expect(page.getByTestId(ids.taskRows)).toContainText(text);
      if (!inProgress) await expect(page.getByTestId(ids.taskRows)).not.toContainText('yet');
      await expect(page.getByTestId(ids.taskRow)).toHaveCount(0);
      await expect(page.getByTestId(ids.railEmpty)).toHaveText('Nothing to measure yet');
      await page.getByTestId(ids.periodTabFeatures).click();
      await expect(page.getByTestId(ids.featureRows)).toContainText(text);
      await expect(page.getByTestId(ids.featureRow)).toHaveCount(0);
    });
  }
}

test('PeriodView_WhenNextIsClicked_ShouldReadTheNextSprint', async ({ mount, page }) => {
  const server = await open(mount, page);
  await page.getByTestId(ids.periodNext).click();
  await expect
    .poll(() => server.reads.at(-1))
    .toBe(
      `/api/projects/id-SHIP/inspection-metrics?range=last6&cut=none&period=${DETAIL.next!.id}`,
    );
});
