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

// A query matches keys only when it has a digit (the server's KeyPart), so a word such as "ship" leaves the key unmarked.
const hasDigit = (query: string): boolean => /\d/.test(query);

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
      {/* At touch the cluster's children are placed on the row's own grid: the key under the title, the state and the
          parent path right-aligned. */}
      <span className="flex items-center justify-end gap-3 text-[length:var(--text-xs)] text-ink-muted touch:contents">
        {item.parentPath.length > 0 ? (
          <span className="touch:col-start-3 touch:row-start-2 touch:justify-self-end">
            {parentPathLabel(item.parentPath)}
          </span>
        ) : null}
        <span className="inline-flex items-center touch:col-start-3 touch:row-start-1 touch:justify-self-end">
          <StateDot category={item.stateCategory} name={item.state} />
        </span>
        <span
          data-testid={ids.optionKey}
          className="min-w-[62px] text-right font-mono text-ink touch:col-start-2 touch:row-start-2 touch:min-w-0 touch:justify-self-start touch:text-left"
        >
          {hasDigit(query) ? <Match text={item.key} query={query} /> : item.key}
        </span>
      </span>
      {match ? <WhereLine match={match} query={query} /> : null}
    </>
  );
}
