import type { BoardLevel } from '../../api/boards';
import { Dialog } from '../../components/Dialog';
import { BoardSettingsView } from './BoardSettingsView';
import { boardPaneGroups, DEFAULT_BOARD_PANE } from './panes';
import { boardSettingsTestIds } from './testIds';
import { useBoardOfLevel } from './useBoardOfLevel';

const levelNames: Record<BoardLevel, string> = {
  stories: 'Stories',
  features: 'Features',
  epics: 'Epics',
  taskboard: 'Taskboard',
};

/**
 * Board settings of one level: changes are held until Save. Finds the board's id by level through the project's boards
 * list. Wide enough for the column state matrix.
 */
export function BoardSettingsDialog({
  projectId,
  level,
  section,
  onSectionChange,
  onClose,
}: {
  projectId: string;
  level: BoardLevel;
  section: string | undefined;
  onSectionChange(section: string): void;
  onClose(): void;
}) {
  const { boardId, failed } = useBoardOfLevel(projectId, level);
  const title = `${levelNames[level]} board settings`;
  const width = '1080px';

  if (boardId === undefined) {
    return (
      <Dialog title={title} width={width} onClose={onClose}>
        {failed ? (
          <p data-testid={boardSettingsTestIds.loadFailed} role="alert" className="text-alert">
            The settings could not be loaded.
          </p>
        ) : (
          <p data-testid={boardSettingsTestIds.loading} className="text-ink-muted">
            Loading settings…
          </p>
        )}
      </Dialog>
    );
  }
  return (
    <BoardSettingsView
      projectId={projectId}
      boardId={boardId}
      title={title}
      width={width}
      groups={boardPaneGroups}
      apply="onSave"
      section={section}
      defaultSection={DEFAULT_BOARD_PANE}
      onSectionChange={onSectionChange}
      onClose={onClose}
    />
  );
}
