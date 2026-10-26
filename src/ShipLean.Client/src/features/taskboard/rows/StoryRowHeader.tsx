import { useState } from 'react';
import type { BoardTypeStatesDto } from '../../../api/boards';
import type { CardFieldSettingJson } from '../../../api/persisted';
import type { CardDto } from '../../../api/views';
import { Avatar } from '../../../components/Avatar';
import { ItemLink } from '../../itemPeek/ItemLink';
import { CardExtraFields } from '../../board/cards/CardExtraFields';
import { StateDot } from '../../../components/StateDot';
import { TypeIcon } from '../../../components/TypeIcon';
import { taskboardTestIds as ids } from '../testIds';
import { RowToggle } from './RowToggle';

/** The header card of a story or bug row: key, points, title, a state picker over the type's states, and the task roll-up. */
export function StoryRowHeader({
  parent,
  parentStates,
  fields,
  showProgress,
  collapsed,
  offerResolve,
  onToggle,
  onSetState,
  onMoveToResolved,
}: {
  projectKey: string;
  parent: CardDto;
  parentStates: BoardTypeStatesDto[];
  /** The Taskboard's Fields settings for this story or bug. */
  fields: Pick<
    CardFieldSettingJson,
    'showKey' | 'showPoints' | 'showAssignee' | 'showEmptyFields' | 'extraFields'
  >;
  /** The Taskboard's annotations setting for this type: the ☑ progress. */
  showProgress: boolean;
  collapsed: boolean;
  /** Every task is closed and the story is not yet Resolved. */
  offerResolve: boolean;
  onToggle: () => void;
  onSetState: (stateId: string) => void;
  /** Sets the story to its first Resolved state; null when its type has none. */
  onMoveToResolved: (() => void) | null;
}) {
  const [dismissed, setDismissed] = useState(false);
  const states = parentStates.find((s) => s.type === parent.type.id)?.states ?? [];
  const tasks = parent.rollups.find((r) => r.type === 'task');
  const total = tasks ? tasks.open + tasks.closed : 0;
  if (collapsed) {
    return (
      <div data-testid={ids.rowHeader} className="flex items-center gap-2 px-2 py-2">
        <RowToggle collapsed onToggle={onToggle} label={parent.key} />
        <TypeIcon type={parent.type.id} />
        <span
          data-testid={ids.rowTitle}
          className="flex-1 truncate text-[length:var(--text-sm)] text-ink"
        >
          <ItemLink itemKey={parent.key} className="text-inherit no-underline hover:underline">
            {parent.title}
          </ItemLink>
        </span>
        <StateDot category={parent.state.category} name={parent.state.name} />
      </div>
    );
  }
  return (
    <div data-testid={ids.rowHeader} className="grid gap-1 px-2 py-2">
      <div className="flex items-center gap-2 text-[length:var(--text-xs)] text-ink-muted">
        <RowToggle collapsed={false} onToggle={onToggle} label={parent.key} />
        <TypeIcon type={parent.type.id} />
        {fields.showKey ? <span>{parent.key}</span> : null}
        <span className="flex-1" />
        {fields.showPoints && (parent.points !== null || fields.showEmptyFields) ? (
          <span data-testid={ids.rowPoints}>{parent.points ?? '–'} pts</span>
        ) : null}
      </div>
      <ItemLink
        testId={ids.rowTitle}
        itemKey={parent.key}
        className="text-[length:var(--text-sm)] text-ink"
      >
        {parent.title}
      </ItemLink>
      <div className="flex items-center gap-2 text-[length:var(--text-xs)]">
        {fields.extraFields.includes('state') ? (
          <select
            data-testid={ids.rowState}
            aria-label={`State of ${parent.key}`}
            className="field"
            value={parent.state.id}
            onChange={(event) => onSetState(event.target.value)}
          >
            {states.map((state) => (
              <option key={state.id} value={state.id}>
                {state.name}
              </option>
            ))}
          </select>
        ) : null}
        {fields.showAssignee && (parent.assignee !== null || fields.showEmptyFields) ? (
          <Avatar {...(parent.assignee ?? {})} />
        ) : null}
        {total === 0 ? (
          <span data-testid={ids.rowNoTasks} className="text-ink-muted">
            No tasks yet
          </span>
        ) : showProgress ? (
          <span data-testid={ids.rowProgress} className="text-ink-muted">
            ☑ {tasks?.closed}/{total}
          </span>
        ) : null}
      </div>
      <CardExtraFields fields={parent.extraFields} />
      {offerResolve && onMoveToResolved && !dismissed ? (
        <div
          data-testid={ids.rowOffer}
          className="flex items-center gap-1.5 rounded-control border border-good bg-sunken px-2 py-1 text-[length:var(--text-xs)]"
        >
          <b>All tasks done.</b>
          <span className="flex-1" />
          <button
            type="button"
            data-testid={ids.rowOfferResolve}
            className="btn-signal"
            onClick={onMoveToResolved}
          >
            Move story to Resolved
          </button>
          <button
            type="button"
            data-testid={ids.rowOfferDismiss}
            aria-label="Dismiss"
            className="btn-ghost"
            onClick={() => setDismissed(true)}
          >
            ✕
          </button>
        </div>
      ) : null}
    </div>
  );
}
