import { ToastHost } from '../../../../components/ToastHost';
import { sprintRetro } from '../../../retros/ct/retroFixtures';
import { RetroActionDialogProvider } from '../../actionDialog/RetroActionDialogProvider';
import type { AddTarget } from '../AddTarget';
import { RetroBoard } from '../RetroBoard';

/** The sprint fixture's columns on a board inside the provider, with a toast host beside it. */
export function RetroBoardHarness({
  hideAll = false,
  highlightedPointId,
  add = { kind: 'item', workItemId: 'w1' },
}: {
  /** A filter that hides every note (a function can't cross from the test to the page). */
  hideAll?: boolean;
  highlightedPointId?: string;
  add?: AddTarget;
}) {
  return (
    <RetroActionDialogProvider projectId="p1">
      <RetroBoard
        columns={sprintRetro.categories}
        groupByTheme
        filter={() => !hideAll}
        note={{ highlightedPointId }}
        add={add}
        scope={{ kind: 'project' }}
      />
      <ToastHost />
    </RetroActionDialogProvider>
  );
}
