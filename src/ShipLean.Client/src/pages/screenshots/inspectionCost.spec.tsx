import { expect } from '@playwright/experimental-ct-react';
import {
  routeInspectionMetrics,
  stubCostOff,
  stubCostOn,
  stubCostUnavailable,
  type stubView,
} from '../../features/inspectionMetrics/ct/stubInspectionMetrics';
import { inspectionMetricsTestIds as ids } from '../../features/inspectionMetrics/testIds';
import { pageShots, type Page } from './shoot';
import { stubShell } from './shellStub';

// Inspection's Cost view (design/screens/inspection.html?view=cost): the cards, tokens per task per sprint, the sprint rows,
// the tasks with the most tokens and tokens by Feature; the view with cost unavailable and with agent runs off.
const ROUTE = '/p/SHIP/inspection?view=cost';

const arrange = (view: ReturnType<typeof stubView>) => async (page: Page) => {
  await stubShell(page);
  await routeInspectionMetrics(page, view);
};

pageShots([
  {
    name: 'inspection-cost',
    route: ROUTE,
    shell: true,
    arrange: arrange(stubCostOn()),
    ready: (p) => expect(p.getByTestId(ids.costFeatureRows)).toBeVisible(),
  },
  {
    name: 'inspection-cost-unavailable',
    route: ROUTE,
    shell: true,
    arrange: arrange(stubCostUnavailable()),
    ready: (p) => expect(p.getByTestId(ids.costUnavailable)).toBeVisible(),
  },
  {
    name: 'inspection-cost-off',
    route: ROUTE,
    shell: true,
    arrange: arrange(stubCostOff()),
    ready: (p) => expect(p.getByTestId(ids.costOff)).toBeVisible(),
  },
]);
