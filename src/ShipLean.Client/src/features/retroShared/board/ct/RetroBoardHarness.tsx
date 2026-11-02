import { sprintRetro } from '../../../retros/ct/retroFixtures';
import { RetroActionDialogProvider } from '../../actionDialog/RetroActionDialogProvider';
import { RetroBoard } from '../RetroBoard';

/** The sprint fixture's columns on a board inside the provider. */
export function RetroBoardHarness({
  hideAll = false,
  highlightedPointId,
}: {
  /** A filter that hides every note (a function can't cross from the test to the page). */
  hideAll?: boolean;
  highlightedPointId?: string;
}) {
  return (
    <RetroActionDialogProvider projectId="p1">
      <RetroBoard
        columns={sprintRetro.categories}
        groupByTheme
        filter={() => !hideAll}
        note={{ highlightedPointId }}
        add={{ kind: 'item', workItemId: 'w1' }}
        scope={{ kind: 'project' }}
      />
    </RetroActionDialogProvider>
  );
}
