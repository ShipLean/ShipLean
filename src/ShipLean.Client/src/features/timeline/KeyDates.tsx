import type { TimelineGapDto, TimelineKeyDateDto } from '@shiplean/core/api/views';
import { timelineTestIds as ids } from './testIds';
import type { KeyDatesProps } from './KeyDatesProps';

const POINT_NAMES = {
  added: 'Added',
  planned: 'Planned',
  executionStarted: 'Execution started',
  finished: 'Finished',
} as const;

// The line under a point's day: the name alone when compact, else what happened and who did it. An unreached Finished
// reads how long execution has run so far.
function detail(
  point: TimelineKeyDateDto,
  compact: boolean,
  executionGap: TimelineGapDto | undefined,
) {
  if (point.at === null) {
    return point.point === 'finished' && executionGap?.soFar && executionGap.label !== null
      ? `${executionGap.label} so far`
      : '';
  }
  if (compact) return point.by?.name ?? '';
  return [point.source, point.by?.name].filter(Boolean).join(' · ');
}

function Point({
  point,
  compact,
  executionGap,
}: {
  point: TimelineKeyDateDto;
  compact: boolean;
  executionGap: TimelineGapDto | undefined;
}) {
  const pending = point.at === null;
  const exec = point.point === 'executionStarted' || point.point === 'finished';
  return (
    <div
      role="listitem"
      data-testid={ids.keyDatePoint}
      data-point={point.point}
      className="relative z-[1] grid min-w-24 max-w-48 justify-items-center gap-px text-center"
    >
      <span
        className={`mt-1 mb-1 size-3 rounded-full ${
          pending
            ? 'border-2 border-[var(--chart-completed)] bg-panel'
            : exec
              ? 'bg-[var(--chart-completed)]'
              : 'bg-ink-muted'
        }`}
      />
      <span className="text-[length:var(--text-2xs)] font-semibold tracking-wide whitespace-nowrap text-ink-muted uppercase">
        {POINT_NAMES[point.point]}
      </span>
      <span
        className={`text-[length:var(--text-sm)] whitespace-nowrap ${pending ? 'text-ink-muted' : 'font-semibold'}`}
      >
        {pending ? 'Not yet' : point.dayLabel}
      </span>
      <span className="text-[length:var(--text-xs)] leading-snug text-ink-muted">
        {detail(point, compact, executionGap)}
      </span>
    </div>
  );
}

function Gap({ gap }: { gap: TimelineGapDto }) {
  const exec = gap.to === 'finished';
  return (
    <div
      data-testid={ids.keyDateGap}
      data-from={gap.from}
      data-to={gap.to}
      className="relative mx-[-2rem] h-[22px] min-w-10"
    >
      <span
        aria-hidden="true"
        className={`absolute inset-x-0 ${
          exec
            ? `top-[9px] border-t-[3px] border-[var(--chart-completed)] ${gap.soFar ? 'border-dashed' : 'border-solid'}`
            : 'top-2.5 border-t-2 border-dotted border-ink-muted'
        }`}
      />
      {gap.label === null ? null : (
        <span
          className={`absolute top-[-9px] left-1/2 -translate-x-1/2 bg-panel px-1.5 text-[length:var(--text-xs)] whitespace-nowrap tabular-nums ${
            exec ? 'font-semibold text-ink' : 'text-ink-muted'
          }`}
        >
          {gap.soFar ? `${gap.label} so far` : gap.label}
        </span>
      )}
    </div>
  );
}

/** The four key dates with the three gaps between them (not to scale; the execution gap is the one in colour). */
export function KeyDates({ view, compact }: KeyDatesProps) {
  const executionGap = view.gaps.find((g) => g.to === 'finished');
  return (
    <div
      role="list"
      aria-label="Key dates"
      data-testid={ids.keyDates}
      data-compact={compact}
      className="grid max-w-[1100px] grid-cols-[auto_minmax(40px,1fr)_auto_minmax(40px,1fr)_auto_minmax(80px,2fr)_auto] items-start overflow-x-auto"
    >
      {view.keyDates.map((point, i) => {
        const gap = view.gaps[i];
        return (
          <div key={point.point} className="contents">
            <Point point={point} compact={compact} executionGap={executionGap} />
            {gap === undefined ? null : <Gap gap={gap} />}
          </div>
        );
      })}
    </div>
  );
}
