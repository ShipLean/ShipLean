import { useMemo, useRef, useState } from 'react';
import { Dialog } from '../../components/Dialog';
import { BoardSettingsView } from '../boardSettings/BoardSettingsView';
import { useBoardOfLevel } from '../boardSettings/useBoardOfLevel';
import { HeldRulesContext } from './panes/styles/HeldRulesContext';
import { DEFAULT_TASKBOARD_PANE, taskboardPaneGroups } from './panes';
import { taskboardSettingsTestIds } from './testIds';

/** Taskboard settings: every change is saved at once. The board is the project's Taskboard-level board. */
export function TaskboardSettingsDialog({
  projectId,
  section,
  onSectionChange,
  onClose,
}: {
  projectId: string;
  section: string | undefined;
  onSectionChange(section: string): void;
  onClose(): void;
}) {
  const { boardId, failed } = useBoardOfLevel(projectId, 'taskboard');
  const title = 'Taskboard settings';
  const held = useRef(new Set<string>());
  const heldRules = useMemo(
    () => ({
      report(ruleId: string, isHeld: boolean) {
        if (isHeld) held.current.add(ruleId);
        else held.current.delete(ruleId);
      },
    }),
    [],
  );
  const [asking, setAsking] = useState(false);
  const requestClose = () => (held.current.size > 0 ? setAsking(true) : onClose());
  const width = '880px';

  if (boardId === undefined) {
    return (
      <Dialog title={title} width={width} onClose={onClose}>
        {failed ? (
          <p data-testid={taskboardSettingsTestIds.loadFailed} role="alert" className="text-alert">
            The settings could not be loaded.
          </p>
        ) : (
          <p data-testid={taskboardSettingsTestIds.loading} className="text-ink-muted">
            Loading settings…
          </p>
        )}
      </Dialog>
    );
  }
  return (
    <HeldRulesContext.Provider value={heldRules}>
      <BoardSettingsView
        projectId={projectId}
        boardId={boardId}
        title={title}
        width={width}
        groups={taskboardPaneGroups}
        apply="immediately"
        section={section}
        defaultSection={DEFAULT_TASKBOARD_PANE}
        onSectionChange={onSectionChange}
        onClose={requestClose}
      />
      {asking ? (
        <Dialog
          title="Unfinished style rule"
          onClose={() => setAsking(false)}
          footer={
            <>
              <button
                type="button"
                data-testid={taskboardSettingsTestIds.keepEditing}
                className="btn-signal"
                onClick={() => setAsking(false)}
              >
                Keep editing
              </button>
              <button
                type="button"
                data-testid={taskboardSettingsTestIds.discardAndClose}
                className="btn-ghost"
                onClick={onClose}
              >
                Discard and close
              </button>
            </>
          }
        >
          <p data-testid={taskboardSettingsTestIds.unfinishedRule}>
            A style rule has criteria that are not valid yet, so it was not saved. Closing now
            discards it.
          </p>
        </Dialog>
      ) : null}
    </HeldRulesContext.Provider>
  );
}
