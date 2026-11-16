import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { TaskChangesDto, TaskCommitsSectionDto } from '@shiplean/core/api/taskChanges';

// The part of Playwright's Page and Route these stubs use (the CT package does not re-export the types).
interface StubRoute {
  fulfill(response: { status: number; contentType: string; body: string }): Promise<void>;
}
interface Page {
  route(url: string, handler: (route: StubRoute) => Promise<void>): Promise<unknown>;
}

const json = (body: unknown) => ({
  status: 200,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

// Stubs the work item read and the two Changes reads, and counts the Changes reads so a test can see a request that should not happen.
export async function stubChanges(
  page: Page,
  item: WorkItemDto,
  changes: TaskChangesDto,
  section: TaskCommitsSectionDto,
) {
  const reads = { changes: 0, section: 0 };
  await page.route('**/api/projects/p1/work-items/by-key/*', (route) => route.fulfill(json(item)));
  await page.route(`**/api/work-items/${item.id}/changes?*`, (route) => {
    reads.changes += 1;
    return route.fulfill(json(changes));
  });
  await page.route(`**/api/work-items/${item.id}/commits`, (route) => {
    reads.section += 1;
    return route.fulfill(json(section));
  });
  return reads;
}
