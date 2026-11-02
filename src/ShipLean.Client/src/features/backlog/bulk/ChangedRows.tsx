import type { ConflictDto } from '../../../api/workItems';
import { backlogTestIds as ids } from '../testIds';

/**
 * Rows a teammate changed while the run was going: what the run did to the others, and the two ways out. An Undo
 * leaves out a changed row the same way, but never offers to overwrite it.
 */
export function ChangedRows({
  summary,
  conflicts,
  canInclude,
  onInclude,
  onLeave,
}: {
  summary: string;
  conflicts: readonly ConflictDto[];
  canInclude: boolean;
  onInclude: () => void;
  onLeave: () => void;
}) {
  return (
    <div
      data-testid={ids.bulkChanged}
      role="alert"
      className="sticky top-0 z-[5] flex flex-none flex-wrap items-center gap-2 border-b border-edge border-l-[3px] px-[var(--space-4)] py-1.5 text-[length:var(--text-sm)] text-ink"
      style={{
        borderLeftColor: 'var(--color-warn)',
        background: 'color-mix(in srgb, var(--color-warn) 12%, var(--color-panel))',
      }}
    >
      <span>
        <b>{summary}.</b>
      </span>
      {conflicts.map((conflict) => (
        <span
          key={conflict.current.id}
          data-testid={ids.bulkChangedRow}
          data-key={conflict.current.key}
        >
          {conflict.current.key} wasn&apos;t included:{' '}
          {conflict.current.changedBy?.name ?? 'A teammate'} changed it while you were working.
        </span>
      ))}
      <span className="ml-auto flex gap-2">
        {canInclude ? (
          <button
            type="button"
            data-testid={ids.bulkIncludeAnyway}
            className="btn-signal"
            onClick={onInclude}
          >
            Include it anyway
          </button>
        ) : null}
        <button type="button" data-testid={ids.bulkLeave} className="btn-ghost" onClick={onLeave}>
          Leave it
        </button>
      </span>
    </div>
  );
}
