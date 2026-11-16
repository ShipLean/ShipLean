import type { ProjectDto } from '@shiplean/core/api/projects';
import type { AddLinkType } from '../routes';
import { LinksEditor } from '../features/workItem/links/LinksEditor';

export interface LinksSlotProps {
  project: ProjectDto;
  itemKey: string;
  addLink?: AddLinkType;
}

// The work item's links, read and written through the form's draft: it renders inside the form's links node.
export function LinksSlot({ project, itemKey, addLink }: LinksSlotProps) {
  return <LinksEditor project={project} itemKey={itemKey} addLink={addLink} />;
}
