import type { RolloverItemDto } from '@shiplean/core/api/sprints';
import type { SprintRef } from '@shiplean/core/api/refs';
import { Avatar } from '../../../components/Avatar';
import { Chip } from '../../../components/Chip';
import { StateDot } from '../../../components/StateDot';
import { TypeIcon } from '../../../components/TypeIcon';
import { useTouchShell } from '../../../touch/useTouchShell';
import { sprintsTestIds as ids } from '../testIds';
import type { Destination } from './rolloverChoices';

interface UnfinishedRowProps {
  item: RolloverItemDto;
  sprint: SprintRef;
  nextSprint: SprintRef | null;
  destination: Destination;
  onChange: (destination: Destination) => void;
}

/** The where-it-goes options for an unfinished item; also the Set all to select's at 900px and below. */
export function DestinationOptions({
  sprint,
  nextSprint,
}: {
  sprint: SprintRef;
  nextSprint: SprintRef | null;
}) {
  return (
    <>
      {nextSprint ? <option value="next">{`Move to ${nextSprint.name}`}</option> : null}
      <option value="backlog">Back to top of backlog</option>
      <option value="stay">{`Leave in ${sprint.name}`}</option>
    </>
  );
}

/**
 * One unfinished story or bug: what it is, where it stands, and a menu for where it goes. At 900px and below it is a
 * card (SHIP-724 D9 design): the type and the whole title, then key · points · tasks open, then a full-width select.
 */
export function UnfinishedRow({
  item,
  sprint,
  nextSprint,
  destination,
  onChange,
}: UnfinishedRowProps) {
  const { touch } = useTouchShell();
  const select = (className: string) => (
    <select
      data-testid={ids.endDestination}
      aria-label={`Where ${item.key} goes`}
      className={className}
      value={destination}
      onChange={(event) => onChange(event.target.value as Destination)}
    >
      <DestinationOptions sprint={sprint} nextSprint={nextSprint} />
    </select>
  );
  if (touch) {
    return (
      <div
        data-testid={ids.endRow}
        data-key={item.key}
        className="grid grid-cols-[auto_minmax(0,1fr)] items-center gap-x-2 gap-y-1 rounded-panel border border-edge bg-panel p-3"
      >
        <TypeIcon type={item.type} />
        <span data-testid={ids.endRowTitle} className="min-w-0 font-medium break-words">
          {item.title}
        </span>
        <span className="col-start-2 flex flex-wrap gap-x-3 text-[length:var(--text-sm)] text-ink-muted">
          <span>{item.key}</span>
          <span>{`${item.points ?? 0} pts`}</span>
          <span>{`${item.openTasks} of ${item.totalTasks} tasks open`}</span>
        </span>
        <div className="col-start-2 mt-1">{select('field w-full')}</div>
      </div>
    );
  }
  return (
    <div
      data-testid={ids.endRow}
      data-key={item.key}
      className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-3 border-b border-edge py-2"
    >
      <div className="grid min-w-0 gap-0.5">
        <div className="flex min-w-0 items-center gap-2">
          <TypeIcon type={item.type} />
          <span className="text-ink-muted">{item.key}</span>
          <span data-testid={ids.endRowTitle} className="truncate font-medium">
            {item.title}
          </span>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <StateDot category={item.state.category} name={item.state.name} />
          <Chip>{`${item.points ?? 0} pts`}</Chip>
          <Chip>{`${item.openTasks} of ${item.totalTasks} tasks open`}</Chip>
          {item.assignee ? <Avatar {...item.assignee} /> : null}
        </div>
      </div>
      {select('field w-[220px]')}
    </div>
  );
}
