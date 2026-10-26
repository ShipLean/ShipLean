import type { ProjectDto } from '../api/projects';
import { BoardSettingsDialog } from '../features/boardSettings/BoardSettingsDialog';
import type { Level } from '../routes';

export interface BoardSettingsSlotProps {
  project: ProjectDto;
  level: Level;
  section: string | undefined;
  onSectionChange(section: string): void;
  onClose(): void;
}

/** The board settings dialog of the page's level, open while the page has a section chosen. */
export function BoardSettingsSlot({
  project,
  level,
  section,
  onSectionChange,
  onClose,
}: BoardSettingsSlotProps) {
  if (section === undefined) return null;
  return (
    <BoardSettingsDialog
      projectId={project.id}
      level={level}
      section={section}
      onSectionChange={onSectionChange}
      onClose={onClose}
    />
  );
}
