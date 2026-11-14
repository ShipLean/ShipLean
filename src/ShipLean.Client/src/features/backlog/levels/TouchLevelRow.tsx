import type { MouseEvent } from 'react';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { StateDot } from '../../../components/StateDot';
import { TypeIcon } from '../../../components/TypeIcon';
import { useItemPeek } from '../../itemPeek/useItemPeek';
import { ParentCell } from '../list/ParentCell';
import { backlogTestIds as ids } from '../testIds';
import { countUnder } from './countUnder';
import type { LevelBacklogLevel } from './levelColumns';
import { LevelPoints } from './LevelPoints';
import { LevelProgress } from './LevelProgress';

// Taps on these keep their own behaviour instead of opening the row.
const inside = 'a, button, input, select, textarea, label';

/**
 * One Feature or Epic as a touch row (900px and below, SHIP-716 D1 and D8): the title, a meta line (key, state, parent or
 * Features count), and a progress line (the bar, "n / m done", own points with the sum under it). No handle, as on desktop
 * these rows don't drag. Tapping the row opens the item modal.
 */
export function TouchLevelRow({
  row,
  projectKey,
  level,
}: {
  row: BacklogRowDto;
  projectKey: string;
  level: LevelBacklogLevel;
}) {
  const peek = useItemPeek();
  const featureCount = countUnder(row, 'feature').total;
  const closed = row.state?.category === 'completed';
  const tap = (event: MouseEvent<HTMLDivElement>) => {
    if (event.target instanceof Element && event.target.closest(inside) !== null) return;
    peek.open(row.key);
  };
  return (
    <div
      role="button"
      tabIndex={0}
      aria-label={`${row.key} ${row.title}`}
      data-testid={ids.row}
      data-project-key={projectKey}
      data-key={row.key}
      onClick={tap}
      onKeyDown={(event) => {
        if (event.key === 'Enter' && event.target === event.currentTarget) peek.open(row.key);
      }}
      className={`min-h-[var(--touch-target)] border-b border-edge px-3 py-2 text-[length:var(--text-sm)] ${closed ? 'text-ink-muted' : 'text-ink'}`}
    >
      <div className="flex items-start gap-1.5">
        {row.type ? (
          <span className="mt-0.5 flex-none">
            <TypeIcon type={row.type.id} />
          </span>
        ) : null}
        <span data-testid={ids.title} className="line-clamp-2 min-w-0 font-medium break-words">
          {row.title}
        </span>
      </div>
      <div
        data-testid={ids.levelTouchMeta}
        className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-[length:var(--text-xs)] text-ink-muted"
      >
        <span data-testid={ids.key}>{row.key}</span>
        {row.state ? (
          <span data-testid={ids.state}>
            <StateDot category={row.state.category} name={row.state.name} />
          </span>
        ) : null}
        {level === 'epics' ? (
          <span data-testid={ids.levelFeatureCount}>
            {featureCount} {featureCount === 1 ? 'Feature' : 'Features'}
          </span>
        ) : null}
        {row.parent ? (
          <span className="max-w-full min-w-0 break-words">
            <ParentCell parent={row.parent} />
          </span>
        ) : null}
        {row.parent?.type.id === 'feature' ? (
          <span data-testid={ids.levelNested}>(nested)</span>
        ) : null}
      </div>
      <div
        data-testid={ids.levelTouchProgress}
        className="mt-1 flex items-center justify-between gap-2 text-[length:var(--text-xs)]"
      >
        <LevelProgress row={row} />
        <LevelPoints row={row} />
      </div>
    </div>
  );
}
