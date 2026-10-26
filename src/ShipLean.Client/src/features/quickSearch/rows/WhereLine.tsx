// The second line of an item row that matched outside its title.
import type { QuickSearchMatch } from '../types';
import { quickSearchTestIds as ids } from '../testIds';
import { Match } from './Match';

function leadIn(match: QuickSearchMatch): string {
  if (match.in === 'attachment') return `In attachment ${match.name}`;
  return match.in === 'comment' ? 'In a comment' : 'In the description';
}

export function WhereLine({ match, query }: { match: QuickSearchMatch; query: string }) {
  return (
    <span
      data-testid={ids.where}
      data-in={match.in}
      className="col-span-2 col-start-2 min-w-0 truncate text-[length:var(--text-xs)] text-ink-muted"
    >
      <span className="font-semibold">{leadIn(match)}</span>{' '}
      <Match text={match.snippet} query={query} />
    </span>
  );
}
