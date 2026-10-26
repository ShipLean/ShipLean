import type { ProjectDto } from '../api/projects';
import { LinksEditor } from '../features/workItem/links/LinksEditor';

export interface LinksSlotProps {
  project: ProjectDto;
  itemKey: string;
}

// The work item's links, read and written through the form's draft: it renders inside the form's links node.
export function LinksSlot({ project, itemKey }: LinksSlotProps) {
  return <LinksEditor project={project} itemKey={itemKey} />;
}
