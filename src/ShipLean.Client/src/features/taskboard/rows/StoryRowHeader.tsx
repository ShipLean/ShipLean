import { useState } from 'react';
import { Link } from 'react-router-dom';
import type { BoardTypeStatesDto } from '../../../api/boards';
import type { CardFieldSettingJson } from '../../../api/persisted';
import type { CardDto } from '../../../api/views';
import { Avatar } from '../../../components/Avatar';
import { CardExtraFields } from '../../board/cards/CardExtraFields';
import { StateDot } from '../../../components/StateDot';
import { TypeIcon } from '../../../components/TypeIcon';
import { routePath } from '../../../routes';
import { taskboardTestIds as ids } from '../testIds';
import { RowToggle } from './RowToggle';

/** The index card drawn behind a header's content: inset in the cell, which stays the theme surface. */
function CardLayer({ collapsed }: { collapsed: boolean }) {
  return (
    <span
      aria-hidden
      data-testid={ids.rowCard}
      className={`pointer-events-none absolute -z-10 rounded-sm border border-[var(--paper-edge)] bg-[var(--paper)] shadow-[var(--paper-shadow)] ${
        collapsed ? 'inset-[3px_6px_3px_3px]' : 'inset-[5px_8px_5px_5px]'
      }`}
    />
  );
}

/** The header card of a story or bug row: key, points, title, a state picker over the type's states, and the task roll-up. */
export function StoryRowHeader({
  projectKey,
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
  const paper = `paper relative isolate ${parent.type.id === 'bug' ? 'paper-bug' : ''}`;
  if (collapsed) {
    return (
      <div
        data-testid={ids.rowHeader}
        className={`${paper} flex items-center gap-2 py-2 pl-2.5 pr-3.5`}
      >
        <CardLayer collapsed />
        <RowToggle collapsed onToggle={onToggle} label={parent.key} />
        <TypeIcon type={parent.type.id} />
        <span
          data-testid={ids.rowTitle}
          className="flex-1 truncate text-[length:var(--text-sm)] text-ink"
        >
          {parent.title}
        </span>
        <StateDot category={parent.state.category} name={parent.state.name} />
      </div>
    );
  }
  return (
    <div data-testid={ids.rowHeader} className={`${paper} grid gap-1 py-3 pl-3 pr-4`}>
      <CardLayer collapsed={false} />
      <div className="flex items-center gap-2 text-[length:var(--text-xs)] text-ink-muted">
        <RowToggle collapsed={false} onToggle={onToggle} label={parent.key} />
        <TypeIcon type={parent.type.id} />
        {fields.showKey ? <span>{parent.key}</span> : null}
        <span className="flex-1" />
        {fields.showPoints && (parent.points !== null || fields.showEmptyFields) ? (
          <span data-testid={ids.rowPoints}>{parent.points ?? '–'} pts</span>
        ) : null}
      </div>
      <Link
        data-testid={ids.rowTitle}
        to={routePath('workItem', { key: projectKey, itemKey: parent.key })}
        className="text-[length:var(--text-sm)] text-ink"
      >
        {parent.title}
      </Link>
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
