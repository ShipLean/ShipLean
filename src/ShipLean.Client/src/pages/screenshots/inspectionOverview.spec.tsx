import { expect, test } from '@playwright/experimental-ct-react';
import type { InspectionMetricsViewDto } from '@shiplean/core/api/views';
import {
  routeInspectionMetrics,
  stubLowCoverage,
  stubNoFinishedSprint,
  stubView,
} from '../../features/inspectionMetrics/ct/stubInspectionMetrics';
import {
  DEFAULT_INSPECTION_QUERY,
  type InspectionView,
} from '../../features/inspectionMetrics/inspectionMetricsQuery';
import { JsonView } from '../../features/inspectionMetrics/json/JsonView';
import { OverviewView } from '../../features/inspectionMetrics/overview/OverviewView';
import { inspectionMetricsTestIds as ids } from '../../features/inspectionMetrics/testIds';
import { projectDto } from '../pagesFixtures';

// The Overview and JSON views at the design's page width (design/screens/inspection-wave1, -no-backfill and -json), in
// both themes. Baselines are generated inside mcr.microsoft.com/playwright:v1.62.0-resolute (ignoreSnapshots outside CI).
const shots: {
  name: string;
  view: InspectionMetricsViewDto;
  query: Extract<InspectionView, 'overview' | 'json'>;
  ready: string;
}[] = [
  {
    name: 'inspection-overview',
    view: stubView(),
    query: 'overview',
    ready: ids.chartBar,
  },
  {
    name: 'inspection-overview-no-backfill',
    view: stubLowCoverage(),
    query: 'overview',
    ready: ids.coverageBanner,
  },
  {
    name: 'inspection-overview-empty',
    view: stubNoFinishedSprint(),
    query: 'overview',
    ready: ids.empty,
  },
  { name: 'inspection-json', view: stubView(), query: 'json', ready: ids.jsonBody },
];

for (const theme of ['light', 'dark'] as const) {
  for (const shot of shots) {
    test(`InspectionOverview_WhenRendered_ShouldMatchItsScreenshot_${shot.name}_${theme}`, async ({
      mount,
      page,
    }) => {
      await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
      await page.setViewportSize({ width: 1440, height: 1100 });
      await routeInspectionMetrics(page, shot.view);
      const root = await mount(
        <div className="bg-canvas p-4" style={{ width: 1280 }}>
          {shot.query === 'json' ? (
            <JsonView
              project={projectDto('SHIP')}
              view={shot.view}
              query={{ ...DEFAULT_INSPECTION_QUERY, view: 'json' }}
              onQueryChange={() => undefined}
            />
          ) : (
            <OverviewView
              project={projectDto('SHIP')}
              view={shot.view}
              query={DEFAULT_INSPECTION_QUERY}
              onQueryChange={() => undefined}
            />
          )}
        </div>,
      );
      await expect(page.getByTestId(shot.ready).first()).toBeVisible();
      await expect(root).toHaveScreenshot(`${shot.name}.${theme}.png`, { animations: 'disabled' });
    });
  }
}
