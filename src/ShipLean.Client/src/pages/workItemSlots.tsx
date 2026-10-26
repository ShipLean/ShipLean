import type { ProjectDto } from '../api/projects';
import type { AttachmentSlotProps } from '../slots/AttachmentSlot';
import { DiscussionSlot } from '../slots/DiscussionSlot';
import type { FormSlotProps } from '../slots/FormSlot';
import { LinksSlot } from '../slots/LinksSlot';
import { SideSlot } from '../slots/SideSlot';
import { TasksSlot } from '../slots/TasksSlot';

// The work item page is the form slot, which owns the frame (header and fields strip across the top, the main column
// and the rail below) and lays out the other four where work-item.html draws them.
export function workItemSlots(project: ProjectDto, itemKey: string): { form: FormSlotProps } {
  return {
    form: {
      project,
      itemKey,
      links: <LinksSlot project={project} itemKey={itemKey} />,
      tasks: <TasksSlot project={project} itemKey={itemKey} />,
      discussion: <DiscussionSlot project={project} itemKey={itemKey} />,
      side: <SideSlot project={project} itemKey={itemKey} />,
    },
  };
}

// The attachment page under a work item is one slot.
export function attachmentSlots(
  project: ProjectDto,
  itemKey: string,
  attachmentId: string,
): { attachment: AttachmentSlotProps } {
  return { attachment: { project, itemKey, attachmentId } };
}
