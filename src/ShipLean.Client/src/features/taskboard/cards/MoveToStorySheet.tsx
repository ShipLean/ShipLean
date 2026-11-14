import { useState } from 'react';
import type { CardDto } from '@shiplean/core/api/views';
import { actionSheetRowClass } from '../../../touch/ActionSheet';
import { Sheet } from '../../../touch/Sheet';
import { useTaskboardMoves } from '../moves/TaskboardMovesContext';
import { taskboardTestIds as ids } from '../testIds';

/** Picks the story or bug a task moves to. The task keeps its state, and the sprint never changes. */
export function MoveToStorySheet({
  card,
  rowId,
  onClose,
  onBack,
}: {
  card: CardDto;
  rowId: string;
  onClose: () => void;
  onBack: () => void;
}) {
  const { view, reparent } = useTaskboardMoves();
  const [query, setQuery] = useState('');
  const needle = query.trim().toLowerCase();
  const stories = view.rows
    .flatMap((row) => (row.parent && row.parent.state.category !== 'completed' ? [row.parent] : []))
    .filter(
      (story) =>
        needle === '' ||
        story.title.toLowerCase().includes(needle) ||
        story.key.toLowerCase().includes(needle),
    );
  return (
    <Sheet open title="Move to story" onClose={onClose}>
      <button
        type="button"
        data-testid={ids.actionBack}
        className={`${actionSheetRowClass} text-signal`}
        onClick={onBack}
      >
        ‹ Back
      </button>
      <input
        type="search"
        data-testid={ids.storySearch}
        aria-label="Find a story or bug"
        placeholder="Find a story or bug"
        value={query}
        onChange={(event) => setQuery(event.target.value)}
        className="m-2 min-h-[var(--touch-target)] rounded-control border border-edge bg-panel px-3"
      />
      {stories.map((story) => {
        const here = story.id === card.parent?.id;
        return (
          <button
            key={story.id}
            type="button"
            data-testid={ids.storyOption}
            data-item-key={story.key}
            aria-current={here ? 'true' : undefined}
            className={`${actionSheetRowClass} flex-col items-start justify-center text-ink ${here ? 'bg-signal-soft' : ''}`}
            onClick={() => {
              if (!here) reparent(card, rowId, story.id, 'sheet');
              onClose();
            }}
          >
            <span>{story.title}</span>
            <span className="text-[length:var(--text-xs)] text-ink-muted">
              {story.key} · {story.state.name}
            </span>
          </button>
        );
      })}
      <p className="m-0 px-2 py-2 text-[length:var(--text-xs)] text-ink-muted">
        Moving a task never changes its sprint.
      </p>
    </Sheet>
  );
}
