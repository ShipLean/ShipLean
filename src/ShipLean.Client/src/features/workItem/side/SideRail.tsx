import type { ProjectDto } from '../../../api/projects';
import { workItemTestIds as ids } from '../testIds';
import { useWorkItem } from '../workItemApi';
import { AttachmentsSection } from './AttachmentsSection';
import { HistorySection } from './HistorySection';
import { ImportedFromSection } from './ImportedFromSection';

// The work item's side rail: attachments, history, and where an imported item came from. Links have their own slot.
export function SideRail({ project, itemKey }: { project: ProjectDto; itemKey: string }) {
  const item = useWorkItem(project.id, itemKey).data;
  return (
    <aside
      data-testid={ids.side}
      aria-label="Attachments and history"
      className="grid content-start gap-4 p-4"
    >
      <AttachmentsSection project={project} itemKey={itemKey} />
      <HistorySection project={project} itemKey={itemKey} />
      {item?.originalSystem ? (
        <ImportedFromSection original={item.originalSystem} itemKey={itemKey} />
      ) : null}
    </aside>
  );
}
