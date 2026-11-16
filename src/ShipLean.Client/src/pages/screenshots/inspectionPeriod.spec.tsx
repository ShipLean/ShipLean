import { expect } from '@playwright/experimental-ct-react';
import {
  routeInspectionMetrics,
  stubFeaturesTab,
  stubPeriod,
  stubView,
} from '../../features/inspectionMetrics/ct/stubInspectionMetrics';
import { inspectionMetricsTestIds as ids } from '../../features/inspectionMetrics/testIds';
import { pageShots, type Page } from './shoot';
import { stubShell } from './shellStub';

// Inspection's one-sprint view (design/screens/inspection-period*.png): its tasks with the rail, and its Features tab.
const VIEW = stubPeriod({ endedBy: 'lastCommit' });
const DETAIL = VIEW.periodDetail!;
const ROUTE = `/p/SHIP/inspection?view=period&p=${DETAIL.periodId}`;

const arrange = (view: ReturnType<typeof stubView>) => async (page: Page) => {
  await stubShell(page);
  await routeInspectionMetrics(page, view);
};

pageShots([
  {
    name: 'inspection-period',
    route: `${ROUTE}&select=${DETAIL.selected!.task.key}`,
    shell: true,
    arrange: arrange(VIEW),
    ready: (p) => expect(p.getByTestId(ids.rail)).toBeVisible(),
  },
  {
    name: 'inspection-period-features',
    route: `${ROUTE}&tab=features`,
    shell: true,
    arrange: arrange(stubFeaturesTab()),
    ready: (p) => expect(p.getByTestId(ids.featureRows)).toBeVisible(),
  },
]);
