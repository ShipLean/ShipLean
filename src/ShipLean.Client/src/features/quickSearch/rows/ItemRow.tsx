import { StateDot } from '../../../components/StateDot';
import { TypeIcon } from '../../../components/TypeIcon';
import type { QuickSearchItem, QuickSearchMatch } from '../types';
import { quickSearchTestIds as ids } from '../testIds';
import { Match } from './Match';
import { WhereLine } from './WhereLine';

// The keys of the parents, from the top down; past two levels it shortens to "… › a › b".
export function parentPathLabel(path: readonly string[]): string {
  return path.length > 2 ? `… › ${path.slice(-2).join(' › ')}` : path.join(' › ');
}

export function ItemRow({
  item,
  match,
  query,
}: {
  item: QuickSearchItem;
  match: QuickSearchMatch | null;
  query: string;
}) {
  return (
    <>
      <TypeIcon type={item.type} />
      <span data-testid={ids.optionLabel} className="min-w-0 truncate">
        <Match text={item.title} query={query} />
      </span>
      <span className="flex items-center justify-end gap-3 text-[length:var(--text-xs)] text-ink-muted max-[899px]:col-start-2 max-[899px]:row-start-2 max-[899px]:justify-start">
        {item.parentPath.length > 0 ? <span>{parentPathLabel(item.parentPath)}</span> : null}
        <StateDot category={item.stateCategory} name={item.state} />
        <span data-testid={ids.optionKey} className="min-w-[62px] text-right font-mono text-ink">
          {item.key}
        </span>
      </span>
      {match ? <WhereLine match={match} query={query} /> : null}
    </>
  );
}
