import { useState } from 'react';
import type {
  RetroCategory,
  RetroCategoryColumnDto,
  RetroPointDto,
} from '@shiplean/core/api/retros';
import type { RetroActionScope } from '../actionDialog/RetroActionScope';
import { retroSharedTestIds as ids } from '../testIds';
import type { AddTarget } from './AddTarget';
import type { NoteOptions } from './NoteOptions';
import { RetroColumn } from './RetroColumn';

/** The four category columns over a set of points, in the order the server sent them (K3), wrapping to two columns and then one. One Add editor is open at a time. */
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
  const [adding, setAdding] = useState<RetroCategory | null>(null);
  return (
    <div
      data-testid={ids.retroBoard}
      className="grid grid-cols-1 items-start gap-3 min-[761px]:grid-cols-2 min-[1251px]:grid-cols-4"
    >
      {columns.map((column) => (
        <RetroColumn
          key={column.category}
          column={column}
          groupByTheme={groupByTheme}
          filter={filter}
          note={note}
          add={add}
          scope={scope}
          adding={adding === column.category}
          onOpenAdd={() => setAdding(column.category)}
          onCloseAdd={() => setAdding(null)}
          onAdded={onAdded}
        />
      ))}
    </div>
  );
}
