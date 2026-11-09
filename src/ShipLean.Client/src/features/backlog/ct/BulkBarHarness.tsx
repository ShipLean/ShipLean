import { useEffect, useRef } from 'react';
import type { BacklogViewDto } from '@shiplean/core/api/views';
import { backlogKey } from '../../../queries/backlogKeys';
import { BulkBar } from '../bulk/BulkBar';
import { BulkRunProvider } from '../bulk/BulkRunProvider';
import { SelectionProvider } from '../selection/SelectionProvider';
import { useSelection } from '../selection/useSelection';
import { CommandHarness } from './CommandHarness';

function Select({ view, ids }: { view: BacklogViewDto; ids: string[] }) {
  const selection = useSelection(view.sections.flatMap((s) => s.rows.map((r) => r.id)));
  const done = useRef(false);
  useEffect(() => {
    if (done.current) return;
    done.current = true;
    selection.addMany(ids);
  }, [selection, ids]);
  return null;
}

function Reselect({ ids }: { ids: string[] }) {
  const selection = useSelection();
  return (
    <button type="button" data-testid="harness-reselect" onClick={() => selection.addMany(ids)}>
      Select again
    </button>
  );
}

// CT-only: the bar over a selection made on mount, with CommandHarness's buttons to run `moveMenu` by name.
export function BulkBarHarness({ view, selected }: { view: BacklogViewDto; selected: string[] }) {
  return (
    <CommandHarness>
      <SelectionProvider>
        <BulkRunProvider
          projectId="p1"
          queryKey={backlogKey('p1', 'stories', 'planning', '', false)}
        >
          <Select view={view} ids={selected} />
          <Reselect ids={selected} />
          <BulkBar mode="planning" projectId="p1" projectKey="SHIP" view={view} />
        </BulkRunProvider>
      </SelectionProvider>
    </CommandHarness>
  );
}
