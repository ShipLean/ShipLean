import type { BacklogRowDto } from '../../../api/views';
import { backlogTestIds as ids } from '../testIds';
import { countUnder } from './countUnder';

// Progress over the stories and bugs under the row, nested Features' included: a bar of the closed share and "n / m done".
export function LevelProgress({ row }: { row: BacklogRowDto }) {
  const { closed, total } = countUnder(row, 'story', 'bug');
  return (
    <div data-testid={ids.levelProgress} className="flex items-center gap-[var(--space-2)]">
      <span
        className="flex h-1.5 max-w-[140px] flex-1 overflow-hidden rounded-full bg-sunken"
        title={`${closed} done · ${total - closed} not done`}
      >
        <i
          data-testid={ids.levelProgressDone}
          className="block h-full"
          style={{
            width: total === 0 ? '0%' : `${(closed / total) * 100}%`,
            background: 'var(--state-completed)',
          }}
        />
      </span>
      <span
        data-testid={ids.levelProgressText}
        className="text-[length:var(--text-xs)] text-ink-muted"
      >
        {closed} / {total} done
      </span>
    </div>
  );
}
