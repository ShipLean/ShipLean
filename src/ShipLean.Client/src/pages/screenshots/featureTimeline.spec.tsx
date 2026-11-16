import { expect } from '@playwright/experimental-ct-react';
import { stubFeatureTimeline } from '../../features/timeline/ct/stubFeatureTimeline';
import { timelineTestIds } from '../../features/timeline/testIds';
import { pageShots, type Page } from './shoot';
import { stubShell } from './shellStub';

const FINISHED_ROUTE = '/p/SHIP/items/SHIP-99/timeline';
const RUNNING_ROUTE = '/p/SHIP/items/SHIP-101/timeline';

async function arrange(page: Page, fixture: 'finished' | 'running') {
  await stubShell(page);
  await stubFeatureTimeline(page, { fixture });
}

const chartReady = (page: Page) =>
  expect(page.getByTestId(timelineTestIds.chartBar).first()).toBeVisible();

pageShots([
  {
    name: 'feature-timeline',
    route: FINISHED_ROUTE,
    shell: true,
    arrange: (p) => arrange(p, 'finished'),
    ready: chartReady,
  },
  {
    name: 'feature-timeline-running',
    route: RUNNING_ROUTE,
    shell: true,
    arrange: (p) => arrange(p, 'running'),
    ready: chartReady,
  },
  {
    name: 'feature-timeline-selected',
    route: `${FINISHED_ROUTE}?select=SHIP-192`,
    shell: true,
    arrange: (p) => arrange(p, 'finished'),
    ready: (p) => expect(p.getByTestId(timelineTestIds.railSelected)).toBeVisible(),
  },
  {
    name: 'feature-timeline-json',
    route: `${FINISHED_ROUTE}?view=json`,
    shell: true,
    arrange: (p) => arrange(p, 'finished'),
    ready: (p) => expect(p.getByTestId(timelineTestIds.json)).toBeVisible(),
  },
]);
