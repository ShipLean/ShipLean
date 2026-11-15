import type { ProjectDto } from '@shiplean/core/api/projects';
import { RunPage } from '../../RunPage';

const project = { id: 'p1', key: 'SHIP', name: 'ShipLean', role: 'member' } as ProjectDto;

// The Run page as the route mounts it; the view state comes from the mounted route's query string.
export function RunPageHarness({ runId = 'sess-cc-1' }: { runId?: string }) {
  return <RunPage project={project} itemKey="SHIP-1" runId={runId} />;
}
