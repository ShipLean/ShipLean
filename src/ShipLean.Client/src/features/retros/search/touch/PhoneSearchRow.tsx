import { Avatar } from '../../../../components/Avatar';
import { Chip } from '../../../../components/Chip';
import type { RetroPointDto } from '@shiplean/core/api/retros';
import { retroCategoryColours, retroCategoryLabels } from '../../../retroShared/retroCategories';
import { retrosTestIds as ids } from '../../testIds';
import { Highlight } from '../parts/Highlight';
import { matchesRootCause } from '../parts/matches';

/**
 * One point as a row of three lines (D1): category and key; the point on two lines with the match marked, then the root
 * cause when the search matched it there or a note when only the raw failure log matched; then theme, sprint and
 * author. The whole row opens the retro.
 */
export function PhoneSearchRow({
  point,
  q,
  logOnly,
  selected,
  onSelect,
}: {
  point: RetroPointDto;
  q: string | undefined;
  /** The row matched only through its retro's raw failure log. */
  logOnly: boolean;
  selected: boolean;
  onSelect: () => void;
}) {
  const { author, workItem, theme, sprint } = point;
  const showRootCause = matchesRootCause(point.rootCause, q);
  return (
    <button
      type="button"
      data-testid={ids.retrosRow}
      data-point-id={point.id}
      data-retro-id={point.retroId}
      aria-selected={selected}
      className={`grid min-h-[var(--touch-target)] w-full cursor-pointer gap-0.5 border-0 border-b border-edge px-3 py-2.5 text-left text-ink ${selected ? 'bg-signal-soft' : 'bg-panel'}`}
      onClick={onSelect}
    >
      <span className="flex min-w-0 items-center gap-2 text-[length:var(--text-xs)] text-ink-muted">
        <span className="inline-flex items-center gap-1.5 text-ink">
          <span
            aria-hidden="true"
            className="size-2 flex-none rounded-[2px]"
            style={{ background: retroCategoryColours[point.category] }}
          />
          {retroCategoryLabels[point.category]}
        </span>
        <span aria-hidden="true">·</span>
        <span className="font-mono text-ink">{workItem ? workItem.key : 'Sprint'}</span>
      </span>
      <span className="line-clamp-2 text-[length:var(--text-base)] leading-[1.4]">
        <Highlight text={point.text} q={q} />
      </span>
      {showRootCause && point.rootCause ? (
        <span
          data-testid={ids.retrosRowRootCause}
          className="line-clamp-1 text-[length:var(--text-sm)] text-ink-muted"
        >
          Root cause: <Highlight text={point.rootCause} q={q} />
        </span>
      ) : null}
      {logOnly ? (
        <span
          data-testid={ids.retrosRowLogMatch}
          className="line-clamp-1 text-[length:var(--text-sm)] text-ink-muted"
        >
          Match in the raw failure log
        </span>
      ) : null}
      <span className="flex min-w-0 items-center gap-2 text-[length:var(--text-xs)] text-ink-muted">
        <span className="min-w-0 truncate">
          {theme ? (
            <>
              # <Highlight text={theme.name} q={q} />
            </>
          ) : (
            <i>Unthemed</i>
          )}
        </span>
        {point.recurring ? (
          <span title="Recurring" className="flex-none">
            <Chip tone="warn">↻</Chip>
          </span>
        ) : null}
        <span className="flex-1" />
        {sprint ? <span className="flex-none">{sprint.name}</span> : null}
        <span className="flex-none">
          <Avatar {...author.user} />
        </span>
      </span>
    </button>
  );
}
