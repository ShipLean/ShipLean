import type { BacklogRowDto } from '../../../api/views';
import { backlogTestIds as ids } from '../testIds';
import { countStoryLevelUnder } from './countUnder';

const share = (count: number, total: number) => `${(count / total) * 100}%`;

// Progress over the story-level items under the row, nested Features' included: bands for done, open in the current sprint
// and open in another sprint, then the bare track for open work in no sprint; and "n / m done".
export function LevelProgress({ row }: { row: BacklogRowDto }) {
  const { closed, total, current, other } = countStoryLevelUnder(row);
  const notPlanned = total - closed - current - other;
  return (
    <div data-testid={ids.levelProgress} className="flex items-center gap-[var(--space-2)]">
      <span
        className="flex h-1.5 max-w-[140px] flex-1 overflow-hidden rounded-full bg-sunken"
        title={`${closed} done · ${current} in current sprint · ${other} in another sprint · ${notPlanned} not in a sprint`}
      >
        <i
          data-testid={ids.levelProgressDone}
          className="block h-full"
          style={{
            width: total === 0 ? '0%' : share(closed, total),
            background: 'var(--state-completed)',
          }}
        />
        {current > 0 && (
          <i
            data-testid={ids.levelProgressCurrent}
            className="block h-full"
            style={{ width: share(current, total), background: 'var(--progress-current-sprint)' }}
          />
        )}
        {other > 0 && (
          <i
            data-testid={ids.levelProgressOther}
            className="block h-full"
            style={{ width: share(other, total), background: 'var(--progress-other-sprint)' }}
          />
        )}
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
