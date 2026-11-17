import { expect } from '@playwright/experimental-ct-react';
import {
  routeInspectionMetrics,
  stubView,
} from '../../features/inspectionMetrics/ct/stubInspectionMetrics';
import { inspectionMetricsTestIds as ids } from '../../features/inspectionMetrics/testIds';
import { pageShots, type Page } from './shoot';
import { stubShell } from './shellStub';

// Inspection's Quality view (design/screens/inspection-quality*.png): the cards, bugs by severity per sprint, the sprint
// rows and the bug list; and the view with no bugs in the project.
const ROUTE = '/p/SHIP/inspection?view=quality';

const arrange = (view: ReturnType<typeof stubView>) => async (page: Page) => {
  await stubShell(page);
  await routeInspectionMetrics(page, view);
};

pageShots([
  {
    name: 'inspection-quality',
    route: ROUTE,
    shell: true,
    arrange: arrange(stubView()),
    ready: (p) => expect(p.getByTestId(ids.qualityBugRows)).toBeVisible(),
  },
  {
    name: 'inspection-quality-empty',
    route: ROUTE,
    shell: true,
    arrange: arrange(stubView({ quality: null, bugs: null })),
    ready: (p) => expect(p.getByTestId(ids.qualityEmpty)).toBeVisible(),
  },
]);
