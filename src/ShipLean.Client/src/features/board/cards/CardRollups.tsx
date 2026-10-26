import { TypeIcon } from '../../../components/TypeIcon';
import type { CardAnnotationsJson } from '../../../api/persisted';
import type { WorkItemTypeId } from '../../../api/refs';
import type { RollupCountDto } from '../../../api/views';
import type { Level } from '../../../routes';
import { boardTestIds as ids } from '../testIds';
import { visibleRollups } from './cardStyle';

// The child roll-ups the board's annotations turn on: per child type, closed of all (a Feature's 2/4 stories).
// A Feature or Epic with nothing below it still offers "No stories yet" / "No Features yet" (the server sends only the
// child types that exist), which opens the list to add the first child. In Done, a roll-up that still counts open children turns warn. With `onRollupClick` a roll-up is a button.
export function CardRollups({
  rollups,
  annotations,
  inDone = false,
  level,
  onRollupClick,
}: {
  rollups: RollupCountDto[];
  annotations: CardAnnotationsJson;
  /** The card sits in the board's last column. */
  inDone?: boolean;
  level: Level;
  onRollupClick?: (type: WorkItemTypeId) => void;
}) {
  const shown = visibleRollups(rollups, annotations);
  if (shown.length === 0) {
    if (rollups.length > 0 || !onRollupClick || level === 'stories') return null;
    const type = level === 'epics' ? 'feature' : 'story';
    return (
      <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
        <button
          type="button"
          onClick={() => onRollupClick(type)}
          data-testid={ids.cardRollup}
          data-type={type}
          data-empty={true}
          className="inline-flex items-center gap-1 text-[length:var(--text-2xs)] text-ink-muted"
        >
          {level === 'epics' ? 'No Features yet' : 'No stories yet'}
        </button>
      </div>
    );
  }
  return (
    <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
      {shown.map((rollup) => {
        const total = rollup.open + rollup.closed;
        const warn = inDone && rollup.open > 0;
        const Tag = onRollupClick ? 'button' : 'span';
        return (
          <Tag
            key={rollup.type}
            type={onRollupClick ? 'button' : undefined}
            onClick={onRollupClick ? () => onRollupClick(rollup.type) : undefined}
            data-warn={warn}
            data-testid={ids.cardRollup}
            data-type={rollup.type}
            data-complete={rollup.open === 0}
            title={`${rollup.type}: ${rollup.closed} of ${total} done`}
            className={`inline-flex items-center gap-1 text-[length:var(--text-2xs)] tabular-nums ${
              warn
                ? 'font-semibold text-caution'
                : rollup.open === 0
                  ? 'text-good'
                  : 'text-ink-muted'
            }`}
          >
            <TypeIcon type={rollup.type} />
            {rollup.closed}/{total}
          </Tag>
        );
      })}
    </div>
  );
}
