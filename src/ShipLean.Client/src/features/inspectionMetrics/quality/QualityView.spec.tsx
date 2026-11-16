import { expect, test } from '@playwright/experimental-ct-react';
import type { InspectionMetricsViewDto } from '@shiplean/core/api/views';
import { AppRoutes } from '../../../AppRoutes';
import { stubProjectApi } from '../../../pages/pagesFixtures';
import { routeInspectionMetrics, stubView } from '../ct/stubInspectionMetrics';
import { inspectionMetricsTestIds as ids } from '../testIds';

const ROUTE = '/p/SHIP/inspection?view=quality';
const VIEW = stubView();
// The fixture's last finished sprint, given distinct severity counts so a swapped pair shows: 3 sev 1, 1 sev 2, 1 unset.
const MIXED = VIEW.periods.find((p) => !p.inProgress && (p.quality?.foundBeforeRelease ?? 0) > 0)!;
const RUNNING = VIEW.periods.find((p) => p.inProgress)!;

function mixedSeverities(): InspectionMetricsViewDto {
  const view = stubView();
  return {
    ...view,
    periods: view.periods.map((p) =>
      p.id !== MIXED.id || p.quality === null
        ? p
        : {
            ...p,
            quality: {
              ...p.quality,
              escaping: 5,
              escapingBySeverity: { sev1: 3, sev2: 1, sev3: 0, sev4: 0, unset: 1 },
            },
          },
    ),
  };
}

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];
type Mount = Parameters<Parameters<typeof test>[2]>[0]['mount'];

async function open(mount: Mount, page: Page, view = VIEW, route = ROUTE) {
  await page.setViewportSize({ width: 1440, height: 900 });
  await stubProjectApi(page);
  await routeInspectionMetrics(page, view);
  await mount(<AppRoutes />, { hooksConfig: { route } });
}

const bar = (page: Page, periodId: string) =>
  page.getByTestId(ids.qualityChartBar).and(page.locator(`[data-period-id="${periodId}"]`));
const segment = (page: Page, periodId: string, sev: string) =>
  bar(page, periodId)
    .getByTestId(ids.qualityChartSegment)
    .and(page.locator(`[data-sev="${sev}"]`));
const height = async (locator: ReturnType<Page['getByTestId']>) =>
  (await locator.boundingBox())!.height;

for (const theme of ['light', 'dark'] as const) {
  test.describe(theme, () => {
    test.beforeEach(async ({ page }) => {
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    });

    test(`QualityView_WhenASprintHasSeveralSeverities_ShouldStackItsBarByTheServersCounts_${theme}`, async ({
      mount,
      page,
    }) => {
      await open(mount, page, mixedSeverities());
      await expect(page.getByTestId(ids.qualityChart)).toBeVisible();
      const sev1 = await height(segment(page, MIXED.id, 'sev1'));
      const sev2 = await height(segment(page, MIXED.id, 'sev2'));
      const unset = await height(segment(page, MIXED.id, 'unset'));
      expect(sev1 / sev2).toBeCloseTo(3, 1);
      expect(unset / sev2).toBeCloseTo(1, 1);
      await expect(segment(page, MIXED.id, 'sev3')).toHaveCount(0);
      // Found before release sits on top as its own dashed part, never in the escaping count.
      await expect(segment(page, MIXED.id, 'foundBeforeRelease')).toHaveCount(1);
      await expect(bar(page, MIXED.id)).toContainText('5');
      // Sev 1 is the bug colour itself.
      await expect(segment(page, MIXED.id, 'sev1')).toHaveCSS(
        'background-color',
        theme === 'light' ? 'rgb(204, 61, 61)' : 'rgb(240, 112, 112)',
      );
    });

    test(`QualityView_WhenNoLinkedPrMerged_ShouldShowPer100AsADash_${theme}`, async ({
      mount,
      page,
    }) => {
      expect(RUNNING.quality?.per100Prs).toBeNull();
      await open(mount, page);
      const row = page
        .getByTestId(ids.qualityPeriodRow)
        .and(page.locator(`[data-period-id="${RUNNING.id}"]`));
      await expect(row.locator('[data-kind="per100"]')).toHaveText('—');
      const finished = VIEW.periods.find((p) => p.id === VIEW.headline.finished!.periodId)!;
      const shown = page
        .getByTestId(ids.qualityPeriodRow)
        .and(page.locator(`[data-period-id="${finished.id}"]`));
      await expect(shown.locator('[data-kind="per100"]')).toHaveText(
        String(finished.quality!.per100Prs),
      );
    });

    test(`QualityView_WhenTheFinishedSprintHasNoMergedPr_ShouldShowItsPer100CardAsADash_${theme}`, async ({
      mount,
      page,
    }) => {
      const view = stubView();
      const finished = view.headline.finished!;
      await open(mount, page, {
        ...view,
        headline: {
          ...view.headline,
          finished: {
            ...finished,
            quality: { ...finished.quality!, mergedPrs: null, per100Prs: null },
          },
        },
      });
      const card = page.getByTestId(ids.qualityCard).and(page.locator('[data-kind="per100"]'));
      await expect(card.getByTestId(ids.qualityCardValue)).toHaveText('—');
      await expect(card).toContainText('no linked PR merged');
    });

    test(`QualityView_WhenABugHasACause_ShouldLinkTheTaskAndOfferCausedByOnTheRest_${theme}`, async ({
      mount,
      page,
    }) => {
      await open(mount, page);
      const rows = page.getByTestId(ids.qualityBugRow);
      const bugs = VIEW.bugs!;
      await expect(rows).toHaveCount(bugs.length);
      // Newest filed first: the server sends oldest first.
      await expect(rows.first()).toHaveAttribute('data-key', bugs[bugs.length - 1].key);
      await expect(rows.last()).toHaveAttribute('data-key', bugs[0].key);
      const caused = bugs.find((b) => b.causedByKey !== null)!;
      const row = (key: string) => rows.and(page.locator(`[data-key="${key}"]`));
      await expect(row(caused.key).getByTestId(ids.qualityBugCausedBy)).toHaveAttribute(
        'href',
        `/p/SHIP/items/${caused.causedByKey}`,
      );
      await expect(row(caused.key).getByTestId(ids.qualityBugAddCausedBy)).toHaveCount(0);
      const uncaused = bugs.find((b) => b.causedByKey === null)!;
      await expect(row(uncaused.key).getByTestId(ids.qualityBugAddCausedBy)).toHaveAttribute(
        'href',
        `/p/SHIP/items/${uncaused.key}?addLink=causedBy`,
      );
      await expect(page.getByTestId(ids.qualityBugFoundBeforeRelease)).toHaveCount(
        bugs.filter((b) => b.foundBeforeRelease).length,
      );
    });

    test(`QualityView_WhenQuartersRollUp_ShouldAddARowAfterEachQuartersLastSprint_${theme}`, async ({
      mount,
      page,
    }) => {
      await open(mount, page);
      const shown = [...new Set(VIEW.periods.map((p) => p.quarter))];
      await expect(page.getByTestId(ids.qualityQuarterRow)).toHaveCount(shown.length);
      for (const quarter of VIEW.quarters.filter((q) => shown.includes(q.quarter))) {
        const r = page
          .getByTestId(ids.qualityQuarterRow)
          .and(page.locator(`[data-quarter="${quarter.quarter}"]`));
        await expect(r).toContainText(String(quarter.quality!.escaping));
      }
    });

    test(`QualityView_WhenTheProjectHasNoBugs_ShouldShowTheEmptyState_${theme}`, async ({
      mount,
      page,
    }) => {
      await open(mount, page, stubView({ quality: null, bugs: null }));
      await expect(page.getByTestId(ids.qualityEmpty)).toBeVisible();
      await expect(page.getByTestId(ids.qualityChart)).toHaveCount(0);
    });

    test(`OverviewQualityCard_WhenASprintFinished_ShouldHeadlineItsEscapingBugsAndOpenQuality_${theme}`, async ({
      mount,
      page,
    }) => {
      await open(mount, page, VIEW, '/p/SHIP/inspection');
      const card = page.getByTestId(ids.overviewQualityCard);
      const finished = VIEW.headline.finished!;
      await expect(card.getByTestId(ids.overviewQualityCardValue)).toHaveText(
        String(finished.quality!.escaping),
      );
      await expect(card).toContainText(`${finished.quality!.per100Prs} per 100 merged PRs`);
      await expect(card.getByTestId(ids.overviewQualityCardSoFar)).toContainText(
        VIEW.headline.soFar!.name,
      );
      await card.click();
      await expect(page.getByTestId(ids.quality)).toBeVisible();
    });

    test(`OverviewQualityCard_WhenTheProjectHasNoBugs_ShouldSaySo_${theme}`, async ({
      mount,
      page,
    }) => {
      const view = stubView({ quality: null, bugs: null });
      const none = <T extends { quality: unknown }>(x: T | null) =>
        x === null ? null : { ...x, quality: null };
      await open(
        mount,
        page,
        {
          ...view,
          headline: {
            finished: none(view.headline.finished),
            soFar: none(view.headline.soFar),
            quarterToDate: none(view.headline.quarterToDate),
            lastQuarter: none(view.headline.lastQuarter),
          },
          periods: view.periods.map((p) => ({ ...p, quality: null })),
        },
        '/p/SHIP/inspection',
      );
      await expect(page.getByTestId(ids.overviewQualityEmpty)).toBeVisible();
      await expect(page.getByTestId(ids.overviewQualityCardValue)).toHaveCount(0);
    });
  });
}
