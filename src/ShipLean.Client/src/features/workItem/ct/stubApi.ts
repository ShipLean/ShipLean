import type { WorkItemDto, WorkItemPatch } from '../../../api/workItems';
import { members, sprints, states } from './fixtures';

// The part of Playwright's Page and Route these stubs use (the CT package does not re-export the types).
interface StubRoute {
  fulfill(response: { status: number; contentType: string; body: string }): Promise<void>;
  request(): { postDataJSON(): unknown; url(): string };
}
interface Page {
  route(url: string, handler: (route: StubRoute) => Promise<void>): Promise<unknown>;
}

const json = (body: unknown, status = 200) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

interface StubOptions {
  /** Other items the by-key lookup finds (the link editor's targets). */
  others?: WorkItemDto[];
  /** What `work-items?type=` lists, by type (the parent picker's candidates). */
  byType?: Record<string, WorkItemDto[]>;
}

// Stubs every call the form makes. `patches` collects the PATCH bodies; `respondToPatch` decides the answer.
// `holdPatches()` makes PATCH answers wait until the returned function is called.
export async function stubApi(
  page: Page,
  item: WorkItemDto,
  respondToPatch: (patch: WorkItemPatch) => { status: number; body: unknown } = () => ({
    status: 200,
    body: { id: item.id, key: item.key, revision: item.revision + 1, changes: [], notices: [] },
  }),
  options: StubOptions = {},
) {
  const patches: WorkItemPatch[] = [];
  let current = item;
  let gate: Promise<void> | null = null;
  await page.route('**/api/projects/p1/work-items/by-key/*', (route) => {
    const key = decodeURIComponent(route.request().url().split('/').pop() ?? '');
    const found = key === current.key ? current : options.others?.find((o) => o.key === key);
    return route.fulfill(
      found ? json(found) : json({ error: 'Work item not found.', code: 'not_found' }, 404),
    );
  });
  await page.route('**/api/projects/p1/states', (route) => route.fulfill(json(states)));
  await page.route('**/api/projects/p1/members', (route) => route.fulfill(json(members)));
  await page.route('**/api/projects/p1/sprints', (route) => route.fulfill(json(sprints)));
  await page.route('**/api/projects/p1/work-items?*', (route) => {
    const type = new URL(route.request().url()).searchParams.get('type') ?? '';
    return route.fulfill(json(options.byType?.[type] ?? []));
  });
  await page.route('**/api/work-items/*', async (route) => {
    const patch = route.request().postDataJSON() as WorkItemPatch;
    patches.push(patch);
    const answer = respondToPatch(patch);
    if (gate) await gate;
    if (answer.status === 200) {
      const set = (field: 'title' | 'description' | 'points' | 'priority' | 'tags' | 'severity') =>
        patch.changes[field] ? { [field]: patch.changes[field].value } : {};
      current = {
        ...current,
        ...set('title'),
        ...set('description'),
        ...set('points'),
        ...set('priority'),
        ...set('tags'),
        ...set('severity'),
        revision: current.revision + 1,
      };
    }
    return route.fulfill(json(answer.body, answer.status));
  });
  await page.route('**/api/markdown/render', (route) => {
    const { markdown } = route.request().postDataJSON() as { markdown: string };
    return route.fulfill(json({ markdown, html: `<p>rendered:${markdown}</p>`, images: [] }));
  });
  return {
    patches,
    holdPatches: () => {
      let release = () => {};
      gate = new Promise<void>((resolve) => {
        release = resolve;
      });
      return release;
    },
    setCurrent: (next: WorkItemDto) => {
      current = next;
    },
  };
}
