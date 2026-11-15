import finishedJson from '../../../../../ShipLean.TestSupport/DtoFixtures/views/feature-timeline-view.json' with { type: 'json' };
import runningJson from '../../../../../ShipLean.TestSupport/DtoFixtures/views/feature-timeline-running-view.json' with { type: 'json' };
import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';

// The part of Playwright's Page and Route these stubs use (the CT package does not re-export the types).
interface StubRoute {
  fulfill(response: { status: number; contentType?: string; body?: string }): Promise<void>;
  request(): { postData(): string | null };
}
interface Page {
  route(url: string, handler: (route: StubRoute) => Promise<void>): Promise<unknown>;
}

/** The finished SHIP-99 sample and the running SHIP-101 one, as the server sends them over HTTP. */
export const FINISHED_TIMELINE = finishedJson as unknown as FeatureTimelineViewDto;
export const RUNNING_TIMELINE = runningJson as unknown as FeatureTimelineViewDto;

export type TimelineFixture = 'finished' | 'running' | 'notFound' | 'error';

export interface TimelineUsageSent {
  path: string;
  body: string | null;
}

export interface TimelineServer {
  /** Every usage POST the page made (timeline opened, bar selected), in order. */
  usageSent(): TimelineUsageSent[];
}

const json = (body: unknown, status = 200) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

/** Stubs the timeline GET for the fixture and records the two usage POSTs (C5), which answer 204. */
export async function stubFeatureTimeline(
  page: Page,
  options: { fixture: TimelineFixture },
): Promise<TimelineServer> {
  const sent: TimelineUsageSent[] = [];
  await page.route('**/api/projects/*/work-items/by-key/*/timeline', (route) => {
    switch (options.fixture) {
      case 'finished':
        return route.fulfill(json(FINISHED_TIMELINE));
      case 'running':
        return route.fulfill(json(RUNNING_TIMELINE));
      case 'notFound':
        return route.fulfill(json({ error: 'Not found.', code: 'not_found' }, 404));
      case 'error':
        return route.fulfill(json({ error: 'Something went wrong.', code: 'server_error' }, 500));
    }
  });
  for (const action of ['timeline-opened', 'timeline-bar-selected']) {
    await page.route(`**/api/projects/*/usage/${action}`, (route) => {
      sent.push({ path: `usage/${action}`, body: route.request().postData() });
      return route.fulfill({ status: 204 });
    });
  }
  return { usageSent: () => [...sent] };
}
