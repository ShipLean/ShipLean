import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import { pagesTestIds } from '../features/pages/testIds';
import { TaskboardSettingsDialog } from '../features/taskboardSettings/TaskboardSettingsDialog';
import { useRefreshTaskboardOnSave } from '../features/taskboardSettings/useRefreshTaskboardOnSave';

export interface TaskboardSettingsSlotProps {
  project: ProjectDto;
  sprint: SprintDto;
  sprints: SprintDto[];
  section: string | undefined;
  onSectionChange(section: string): void;
  onClose(): void;
}

/** The Taskboard settings dialog, open while the page has a `?settings=` section. */
export function TaskboardSettingsSlot({
  project,
  sprint,
  section,
  onSectionChange,
  onClose,
}: TaskboardSettingsSlotProps) {
  useRefreshTaskboardOnSave(project.id, sprint.id);
  return (
    <div data-testid={pagesTestIds.slotTaskboardSettings} className="min-h-px">
      {section === undefined ? null : (
        <TaskboardSettingsDialog
          projectId={project.id}
          section={section}
          onSectionChange={onSectionChange}
          onClose={onClose}
        />
      )}
    </div>
  );
}
