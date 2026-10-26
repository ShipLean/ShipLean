import { useState } from 'react';
import type { BoardViewDto } from '../../../api/views';
import type { Level } from '../../../routes';
import { BoardGrid } from '../grid/BoardGrid';
import type { BoardContext } from '../grid/boardContext';
import { BOARD_ID, PROJECT } from './fixtures';

// The grid with a context of its own and a counter of the cursors it was asked to expand (CT only).
export function GridHarness({
  view,
  compact = false,
  flashes = 0,
  level = 'stories',
  flashing = [],
}: {
  view: BoardViewDto;
  compact?: boolean;
  flashes?: number;
  level?: Level;
  /** Ids of the cards flashing. */
  flashing?: string[];
}) {
  const [expanded, setExpanded] = useState<string[]>([]);
  const context: BoardContext = {
    projectId: PROJECT.id,
    projectKey: PROJECT.key,
    boardId: BOARD_ID,
    level,
    compact,
    settings: view,
    flashing: new Set(flashing),
    onExpand: (cursor) => setExpanded((all) => [...all, cursor]),
  };
  return (
    <>
      <BoardGrid view={view} context={context} headerFlashes={flashes} />
      <output data-testid="expanded">{expanded.join(',')}</output>
    </>
  );
}
