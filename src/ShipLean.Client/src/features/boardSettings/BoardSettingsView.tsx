import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Dialog } from '../../components/Dialog';
import { SettingsDialog } from '../../components/SettingsDialog/SettingsDialog';
import { boardSettingsKey, fetchBoardSettings, saveBoardSettings } from './boardSettingsApi';
import type { BoardPaneGroup, BoardSettingsContext } from './panes';
import { boardSettingsTestIds } from './testIds';

/** The part the Board and Taskboard dialogs share: load the board's settings, then show them in the frame. */
export function BoardSettingsView({
  projectId,
  boardId,
  title,
  width,
  groups,
  apply,
  section,
  defaultSection,
  onSectionChange,
  onClose,
}: {
  projectId: string;
  boardId: string;
  title: string;
  width?: string;
  groups: BoardPaneGroup[];
  apply: 'onSave' | 'immediately';
  section: string | undefined;
  defaultSection: string;
  onSectionChange(section: string): void;
  onClose(): void;
}) {
  const queryClient = useQueryClient();
  const settings = useQuery({
    queryKey: boardSettingsKey(projectId, boardId),
    queryFn: () => fetchBoardSettings(boardId),
  });

  if (settings.data === undefined) {
    return (
      <Dialog title={title} width={width} onClose={onClose}>
        {settings.isError ? (
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

  const context: BoardSettingsContext = {
    projectId,
    boardId,
    level: settings.data.level,
    states: settings.data.states,
    warnings: settings.data.warnings,
  };
  return (
    <SettingsDialog
      title={title}
      savedMessage="Board settings saved. Everyone on the board sees the change now"
      width={width}
      groups={groups}
      document={settings.data.settings}
      context={context}
      apply={apply}
      section={section}
      defaultSection={defaultSection}
      onSectionChange={onSectionChange}
      onSave={(draft, base) => saveBoardSettings(queryClient, projectId, boardId, draft, base)}
      onClose={onClose}
    />
  );
}
