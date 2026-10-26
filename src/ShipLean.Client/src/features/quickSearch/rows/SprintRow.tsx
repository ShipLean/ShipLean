import { Match } from './Match';

export function SprintRow({
  name,
  goal,
  datesLabel,
  current,
  projectKey,
  query,
}: {
  name: string;
  goal: string;
  datesLabel: string;
  current: boolean;
  projectKey: string;
  query: string;
}) {
  return (
    <>
      <span aria-hidden="true" className="text-center text-ink-muted">
        ◷
      </span>
      <span className="min-w-0 truncate">
        <Match text={name} query={query} />
        {goal === '' ? null : (
          <span className="text-ink-muted">
            {' · '}
            <Match text={goal} query={query} />
          </span>
        )}
      </span>
      <span className="flex items-center justify-end gap-2 text-[length:var(--text-xs)] text-ink-muted max-[899px]:col-start-2 max-[899px]:row-start-2 max-[899px]:justify-start">
        {current ? (
          <span className="rounded-full bg-signal-soft px-2 py-px font-medium text-ink">
            current
          </span>
        ) : null}
        <span>{datesLabel}</span>
        <span className="min-w-[62px] text-right font-mono text-ink">{projectKey}</span>
      </span>
    </>
  );
}
