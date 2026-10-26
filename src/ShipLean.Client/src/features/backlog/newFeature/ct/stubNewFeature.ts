import type { WorkItemCreate } from '../../../../api/workItems';
import type { SlugCheck } from '../newFeatureApi';

// The part of Playwright's Page and Route these stubs use (the CT package does not re-export the types).
interface StubRoute {
  fulfill(response: { status: number; contentType?: string; body?: string }): Promise<void>;
  request(): { method(): string; url(): string; postDataJSON(): unknown };
}
interface Page {
  route(url: string, handler: (route: StubRoute) => Promise<void>): Promise<unknown>;
}

const json = (body: unknown, status = 200) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

export const FEATURE_STATES = [
  { id: 's-idea', type: 'feature', name: 'Idea', category: 'proposed', order: 1, itemCount: 0 },
  {
    id: 's-active',
    type: 'feature',
    name: 'Active',
    category: 'inProgress',
    order: 2,
    itemCount: 0,
  },
  { id: 's-done', type: 'feature', name: 'Done', category: 'completed', order: 3, itemCount: 0 },
  { id: 's-new', type: 'story', name: 'New', category: 'proposed', order: 1, itemCount: 0 },
];

const epic = {
  id: 'e1',
  key: 'SHIP-10',
  title: 'Checkout conversion',
  type: { id: 'epic', name: 'Epic' },
  parent: null,
};
const feature = {
  id: 'f1',
  key: 'SHIP-11',
  title: 'Reporting',
  type: { id: 'feature', name: 'Feature' },
  parent: { id: 'e2', key: 'SHIP-12', title: 'Insights', type: { id: 'epic', name: 'Epic' } },
};

export interface NewFeatureServer {
  // The slug values asked about, in order.
  checks: string[];
  creates: WorkItemCreate[];
}

// Stubs what the dialog reads and writes: the slug check, the project's states, the Epic candidates and the create.
export async function stubNewFeature(
  page: Page,
  options: {
    // Slugs the check answers `available: false` for.
    taken?: string[];
    // Answers the create instead of a 200 for SHIP-184.
    create?: { status: number; body: unknown };
  } = {},
): Promise<NewFeatureServer> {
  const server: NewFeatureServer = { checks: [], creates: [] };
  await page.route('**/api/projects/p1/features/slug-check?*', (route) => {
    const value = new URL(route.request().url()).searchParams.get('value') ?? '';
    server.checks.push(value);
    const answer: SlugCheck = { value, available: !(options.taken ?? []).includes(value) };
    return route.fulfill(json(answer));
  });
  await page.route('**/api/projects/p1/states', (route) => route.fulfill(json(FEATURE_STATES)));
  await page.route('**/api/projects/p1/work-items?type=epic', (route) =>
    route.fulfill(json([epic])),
  );
  await page.route('**/api/projects/p1/work-items?type=feature', (route) =>
    route.fulfill(json([feature])),
  );
  await page.route('**/api/work-items', (route) => {
    server.creates.push(route.request().postDataJSON() as WorkItemCreate);
    const answer = options.create ?? {
      status: 200,
      body: { id: 'new', key: 'SHIP-184', revision: 1, changes: [], notices: [] },
    };
    return route.fulfill(json(answer.body, answer.status));
  });
  return server;
}
