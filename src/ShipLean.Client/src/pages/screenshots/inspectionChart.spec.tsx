import { expect, test } from '@playwright/experimental-ct-react';
import type { InspectionPeriodDto } from '@shiplean/core/api/views';
import fixture from '../../../../ShipLean.TestSupport/DtoFixtures/views/inspection-metrics-view.json' with { type: 'json' };
import { LeadTimeChart } from '../../features/inspectionMetrics/chart/LeadTimeChart';
import { inspectionMetricsTestIds as ids } from '../../features/inspectionMetrics/testIds';

// The lead-time chart at the design's Lead time panel width (design/screens/inspection.html?view=leadtime), in both
// themes. Baselines are generated inside mcr.microsoft.com/playwright:v1.62.0-resolute (ignoreSnapshots outside CI).
const PERIODS = fixture.periods as unknown as InspectionPeriodDto[];

const shots: { name: string; periods: InspectionPeriodDto[]; height: number; ready: string }[] = [
  { name: 'inspection-chart', periods: PERIODS, height: 230, ready: ids.chartBar },
  { name: 'inspection-chart-overview', periods: PERIODS, height: 200, ready: ids.chartBar },
  { name: 'inspection-chart-empty', periods: [], height: 200, ready: ids.chartEmpty },
];

for (const theme of ['light', 'dark'] as const) {
  for (const shot of shots) {
    test(`LeadTimeChart_WhenRendered_ShouldMatchItsScreenshot_${shot.name}_${theme}`, async ({
      mount,
      page,
    }) => {
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      const chart = await mount(
        <div className="bg-panel p-3" style={{ width: 920 }}>
          <LeadTimeChart
            periods={shot.periods}
            height={shot.height}
            onPeriod={() => undefined}
            onDot={() => undefined}
          />
        </div>,
      );
      await expect(page.getByTestId(shot.ready).first()).toBeVisible();
      await expect(chart).toHaveScreenshot(`${shot.name}.${theme}.png`, { animations: 'disabled' });
    });
  }
}
