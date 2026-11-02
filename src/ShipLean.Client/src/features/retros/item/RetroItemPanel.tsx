import type { ProjectDto } from '../../../api/projects';
import { retrosTestIds as ids } from '../testIds';
import { ItemChildRows } from './ItemChildRows';
import { ItemFailureLogs } from './ItemFailureLogs';
import { ItemRail } from './ItemRail';

// TODO: the item's board and filters replace this empty panel; the three parts below are already mounted.
export function RetroItemPanel({ project, itemKey }: { project: ProjectDto; itemKey: string }) {
  return (
    <div data-testid={ids.retroItemPanel} data-project={project.key} data-item={itemKey}>
      <ItemChildRows />
      <ItemFailureLogs />
      <ItemRail />
    </div>
  );
}
