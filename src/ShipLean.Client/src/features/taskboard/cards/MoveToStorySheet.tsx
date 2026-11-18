import { useState } from 'react';
import type { SprintRef } from '@shiplean/core/api/refs';
import type { CardDto, FeatureBoardRowDto } from '@shiplean/core/api/views';
import { TypeIcon } from '../../../components/TypeIcon';
import {
  ActionSheetGroupHeading,
  ActionSheetRowFace,
  actionSheetCheckedClass,
  actionSheetRowClass,
} from '../../../touch/ActionSheet';
import { Sheet } from '../../../touch/Sheet';
import { useTaskboardMoves } from '../moves/TaskboardMovesContext';
import { taskboardTestIds as ids } from '../testIds';

// The Feature board's rows carry the story's sprint (null = Backlog); the Taskboard's are all in one sprint.
function sprintOf(row: object): SprintRef | null | undefined {
  return 'sprint' in row ? (row as Pick<FeatureBoardRowDto, 'sprint'>).sprint : undefined;
}

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
  const { view, reparent, scope } = useTaskboardMoves();
  const [query, setQuery] = useState('');
  const needle = query.trim().toLowerCase();
  const stories = view.rows
    .flatMap((row) =>
      row.parent && row.parent.state.category !== 'completed'
        ? [{ story: row.parent, sprint: sprintOf(row) }]
        : [],
    )
    .filter(
      ({ story }) =>
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
      {stories.map(({ story, sprint }) => {
        const here = story.id === card.parent?.id;
        return (
          <button
            key={story.id}
            type="button"
            data-testid={ids.storyOption}
            data-item-key={story.key}
            aria-current={here ? 'true' : undefined}
            className={`${actionSheetRowClass} gap-3 text-ink ${here ? actionSheetCheckedClass : ''}`}
            onClick={() => {
              if (!here) reparent(card, rowId, story.id, 'sheet');
              onClose();
            }}
          >
            <ActionSheetRowFace
              face={{
                label: story.title,
                icon: <TypeIcon type={story.type.id} />,
                hint: [
                  story.key,
                  sprint === undefined ? null : (sprint?.name ?? 'Backlog'),
                  story.state.name,
                ]
                  .filter(Boolean)
                  .join(' · '),
                checked: here,
              }}
            />
          </button>
        );
      })}
      {scope.kind === 'sprint' ? <ActionSheetGroupHeading label="" divider /> : null}
      {scope.kind === 'sprint' ? (
        <button
          type="button"
          data-testid={ids.storyNoParent}
          aria-current={!card.parent ? 'true' : undefined}
          className={`${actionSheetRowClass} gap-3 text-ink ${!card.parent ? actionSheetCheckedClass : ''}`}
          onClick={() => {
            if (card.parent) reparent(card, rowId, null, 'sheet');
            onClose();
          }}
        >
          <ActionSheetRowFace
            face={{
              label: 'No parent',
              hint: 'Unparented: carries no sprint points',
              checked: !card.parent,
            }}
          />
        </button>
      ) : null}
      <p className="m-0 px-2 py-2 text-[length:var(--text-xs)] text-ink-muted">
        Moving a task never changes its sprint.
      </p>
    </Sheet>
  );
}
