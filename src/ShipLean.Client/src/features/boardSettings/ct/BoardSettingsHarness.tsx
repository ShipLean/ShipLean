import { useState } from 'react';
import { ToastHost } from '../../../components/ToastHost';
import type { PaneProps } from '../../../components/SettingsDialog/types';
import type { BoardSettingsJson } from '../../../api/persisted';
import { boardHarnessIds } from './harnessIds';
import { BoardSettingsView } from '../BoardSettingsView';
import type { BoardPaneGroup, BoardSettingsContext } from '../panes';

function TogglePane({ draft, onChange }: PaneProps<BoardSettingsJson, BoardSettingsContext>) {
  return (
    <button
      type="button"
      data-testid={boardHarnessIds.toggleTasks}
      onClick={() =>
        onChange({
          ...draft,
          annotations: { ...draft.annotations, tasks: !draft.annotations.tasks },
        })
      }
    >
      Toggle tasks
    </button>
  );
}

function EmptyPane() {
  return null;
}

const groups: BoardPaneGroup[] = [
  {
    label: 'Cards',
    panes: [
      {
        id: 'annotations',
        label: 'Annotations',
        sections: ['annotations'],
        component: TogglePane,
        // computed from the draft, as the real panes' warnings are
        warning: (draft) => (draft.annotations.tasks ? null : '1 annotation hidden'),
      },
      { id: 'swimlanes', label: 'Swimlanes', sections: ['swimlanes'], component: EmptyPane },
    ],
  },
];

/** CT-only: the real loading, saving and frame over one pane that changes the draft (the real panes are stubs). */
export function BoardSettingsHarness({
  projectId,
  boardId,
  apply,
}: {
  projectId: string;
  boardId: string;
  apply: 'onSave' | 'immediately';
}) {
  const [open, setOpen] = useState(true);
  return (
    <>
      <ToastHost />
      {open ? (
        <BoardSettingsView
          projectId={projectId}
          boardId={boardId}
          title="Harness settings"
          groups={groups}
          apply={apply}
          section="annotations"
          defaultSection="annotations"
          onSectionChange={() => undefined}
          onClose={() => setOpen(false)}
        />
      ) : (
        <span data-testid={boardHarnessIds.closed} />
      )}
    </>
  );
}
