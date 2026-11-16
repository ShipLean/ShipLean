import { expect } from '@playwright/experimental-ct-react';
import {
  routeInspectionMetrics,
  stubCut,
  stubView,
} from '../../features/inspectionMetrics/ct/stubInspectionMetrics';
import { inspectionMetricsTestIds as ids } from '../../features/inspectionMetrics/testIds';
import { pageShots, type Page } from './shoot';
import { stubShell } from './shellStub';

// Inspection's Lead time view (design/screens/inspection-leadtime*.png): all tasks, and split by Feature and by who.
const ROUTE = '/p/SHIP/inspection?view=leadtime';

const arrange = (view: ReturnType<typeof stubView>) => async (page: Page) => {
  await stubShell(page);
  await routeInspectionMetrics(page, view);
};

pageShots([
  {
    name: 'inspection-leadtime',
    route: ROUTE,
    shell: true,
    arrange: arrange(stubView()),
    ready: (p) => expect(p.getByTestId(ids.periodRows)).toBeVisible(),
  },
  {
    name: 'inspection-leadtime-feature',
    route: `${ROUTE}&cut=feature`,
    shell: true,
    arrange: arrange(stubCut('feature')),
    ready: (p) => expect(p.getByTestId(ids.cutTable)).toBeVisible(),
  },
  {
    name: 'inspection-leadtime-who',
    route: `${ROUTE}&cut=who`,
    shell: true,
    arrange: arrange(stubCut('who')),
    ready: (p) => expect(p.getByTestId(ids.cutTable)).toBeVisible(),
  },
]);
