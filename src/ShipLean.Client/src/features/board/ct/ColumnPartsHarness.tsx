import type { BoardColumnDto } from '@shiplean/core/api/views';
import type { Level } from '../../../routes';
import { ColumnHeader } from '../grid/ColumnHeader';
import { NewCard } from '../grid/NewCard';
import { ShowOlder } from '../grid/ShowOlder';
import { BOARD_ID, PROJECT, column } from './fixtures';

const STATE = (id: string, name: string, category: 'proposed' | 'inProgress' | 'completed') => ({
  id,
  name,
  category,
});

// The states a column maps per type, by board level: the header names each once.
const COLUMNS: Record<Level, BoardColumnDto> = {
  stories: column({
    id: 'c-stories',
    name: 'In progress',
    wipLimit: 2,
    count: 3,
    states: [
      { type: 'story', state: STATE('s1', 'Active', 'inProgress') },
      { type: 'bug', state: STATE('s2', 'Fixing', 'inProgress') },
    ],
  }),
  features: column({
    id: 'c-features',
    name: 'Delivery',
    count: 4,
    states: [{ type: 'feature', state: STATE('f1', 'Building', 'inProgress') }],
  }),
  epics: column({
    id: 'c-epics',
    name: 'Discovery',
    count: 2,
    states: [{ type: 'epic', state: STATE('e1', 'Shaping', 'proposed') }],
  }),
};

// One column part on its own at the width of a phone's column page (CT only). `over` pushes the column over its WIP.
export function ColumnPartsHarness({
  part,
  level = 'stories',
  over = false,
}: {
  part: 'header' | 'showOlder' | 'newCard';
  level?: Level;
  over?: boolean;
}) {
  const base = COLUMNS[level];
  const shown = over ? { ...base, wipLimit: 2, count: 3, overLimit: true } : base;
  return (
    <div data-testid="column-part" className="grid w-[340px] gap-2 bg-canvas p-2">
      {part === 'header' ? <ColumnHeader column={shown} flashes={0} /> : null}
      {part === 'showOlder' ? <ShowOlder count={4} cursor="done:lane" onExpand={() => {}} /> : null}
      {part === 'newCard' ? (
        <NewCard projectId={PROJECT.id} boardId={BOARD_ID} level={level} />
      ) : null}
    </div>
  );
}
