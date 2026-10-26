import type { RolloverItemDto } from '../../../api/sprints';
import type { SprintRef } from '../../../api/refs';
import { Avatar } from '../../../components/Avatar';
import { Chip } from '../../../components/Chip';
import { StateDot } from '../../../components/StateDot';
import { TypeIcon } from '../../../components/TypeIcon';
import { sprintsTestIds as ids } from '../testIds';
import type { Destination } from './rolloverChoices';

interface UnfinishedRowProps {
  item: RolloverItemDto;
  sprint: SprintRef;
  nextSprint: SprintRef | null;
  destination: Destination;
  onChange: (destination: Destination) => void;
}

/** One unfinished story or bug: what it is, where it stands, and a menu for where it goes. */
export function UnfinishedRow({
  item,
  sprint,
  nextSprint,
  destination,
  onChange,
}: UnfinishedRowProps) {
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
          <span className="truncate font-medium">{item.title}</span>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <StateDot category={item.state.category} name={item.state.name} />
          <Chip>{`${item.points ?? 0} pts`}</Chip>
          <Chip>{`${item.openTasks} of ${item.totalTasks} tasks open`}</Chip>
          {item.assignee ? <Avatar {...item.assignee} /> : null}
        </div>
      </div>
      <select
        data-testid={ids.endDestination}
        aria-label={`Where ${item.key} goes`}
        className="field w-[220px]"
        value={destination}
        onChange={(event) => onChange(event.target.value as Destination)}
      >
        {nextSprint ? <option value="next">{`Move to ${nextSprint.name}`}</option> : null}
        <option value="backlog">Back to top of backlog</option>
        <option value="stay">{`Leave in ${sprint.name}`}</option>
      </select>
    </div>
  );
}
