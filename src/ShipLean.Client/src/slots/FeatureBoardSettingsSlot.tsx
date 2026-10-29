import type { ProjectDto } from '../api/projects';
import { useFeatureBoard } from '../features/featureBoard/featureBoardApi';
import { pagesTestIds } from '../features/pages/testIds';
import type { TaskboardScope } from '../features/taskboard/taskboardScope';
import { TaskboardSettingsDialog } from '../features/taskboardSettings/TaskboardSettingsDialog';
import { useRefreshTaskboardOnSave } from '../features/taskboardSettings/useRefreshTaskboardOnSave';

export interface FeatureBoardSettingsSlotProps {
  project: ProjectDto;
  itemKey: string;
  section: string | undefined;
  onSectionChange(section: string): void;
  onClose(): void;
}

// The Taskboard's settings dialog (the Feature board has no settings of its own), open while the page has a `?settings=` section.
export function FeatureBoardSettingsSlot({
  project,
  itemKey,
  section,
  onSectionChange,
  onClose,
}: FeatureBoardSettingsSlotProps) {
  const { scope } = useFeatureBoard(project.id, itemKey);
  return (
    <div data-testid={pagesTestIds.slotTaskboardSettings} className="min-h-px">
      {scope ? <RefreshOnSave projectId={project.id} scope={scope} /> : null}
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

function RefreshOnSave({ projectId, scope }: { projectId: string; scope: TaskboardScope }) {
  useRefreshTaskboardOnSave(projectId, scope);
  return null;
}
