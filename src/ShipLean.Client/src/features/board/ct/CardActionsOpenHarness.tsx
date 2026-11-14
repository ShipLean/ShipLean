import { useState } from 'react';
import type { BoardViewDto } from '@shiplean/core/api/views';
import { BoardDnd } from '../moves/BoardDnd';
import { useCardActions } from '../cards/touch/CardActionsContext';
import { BOARD_ID, PROJECT } from './fixtures';

function Opener({ cardId }: { cardId: string }) {
  const actions = useCardActions();
  return (
    <button type="button" data-testid="harness-open" onClick={() => actions.open(cardId)}>
      Open
    </button>
  );
}

// A board's moves with a button that opens a card's actions through `useCardActions().open`, the way a long press
// does, and a line saying what the roll-up opener was called with (CT only).
export function CardActionsOpenHarness({ view, cardId }: { view: BoardViewDto; cardId: string }) {
  const [called, setCalled] = useState('');
  return (
    <>
      <BoardDnd
        view={view}
        context={{
          projectId: PROJECT.id,
          boardId: BOARD_ID,
          level: 'features',
          onRollupClick: (id, key, type) => setCalled(`${id} ${key} ${type}`),
        }}
      >
        <Opener cardId={cardId} />
      </BoardDnd>
      <output data-testid="harness-called">{called}</output>
    </>
  );
}
