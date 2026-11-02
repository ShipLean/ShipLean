import type { RetroCategoryColumnDto, RetroPointDto } from '../../../api/retros';
import type { RetroActionScope } from '../actionDialog/RetroActionScope';
import { retroSharedTestIds as ids } from '../testIds';
import type { AddTarget } from './AddTarget';
import type { NoteOptions } from './NoteOptions';
import { RetroColumn } from './RetroColumn';

/** The four category columns over a set of points, in the order the server sent them (K3). */
// TODO: the finished board layout (wrapping to two columns, then one) replaces this plain grid.
export function RetroBoard({
  columns,
  groupByTheme,
  filter,
  note,
  add,
  scope,
  onAdded,
}: {
  columns: RetroCategoryColumnDto[];
  groupByTheme: boolean;
  filter: (point: RetroPointDto) => boolean;
  note: NoteOptions;
  add: AddTarget;
  scope: RetroActionScope;
  onAdded?: (point: RetroPointDto) => void;
}) {
  return (
    <div data-testid={ids.retroBoard} className="grid grid-cols-4 items-start gap-3">
      {columns.map((column) => (
        <RetroColumn
          key={column.category}
          column={column}
          groupByTheme={groupByTheme}
          filter={filter}
          note={note}
          add={add}
          scope={scope}
          onAdded={onAdded}
        />
      ))}
    </div>
  );
}
