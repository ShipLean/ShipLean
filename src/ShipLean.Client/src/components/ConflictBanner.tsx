import { componentTestIds } from './testIds';

/** Shown on a stale-revision conflict: who changed it, and the two ways out. */
export function ConflictBanner({
  changedBy,
  onUseMine,
  onKeepTheirs,
}: {
  changedBy: string;
  onUseMine: () => void;
  onKeepTheirs: () => void;
}) {
  return (
    <div
      data-testid={componentTestIds.conflictBanner}
      role="alert"
      className="flex items-center gap-2 rounded-control border border-edge bg-panel px-3 py-2 text-[length:var(--text-sm)] text-ink"
      style={{ borderLeft: '3px solid var(--color-warn)' }}
    >
      <span>
        <b>{changedBy}</b> changed this while you were editing.
      </span>
      <span className="ml-auto flex gap-2">
        <button
          type="button"
          data-testid={componentTestIds.conflictUseMine}
          className="btn-signal"
          onClick={onUseMine}
        >
          Use mine
        </button>
        <button
          type="button"
          data-testid={componentTestIds.conflictKeepTheirs}
          className="btn-ghost"
          onClick={onKeepTheirs}
        >
          Keep theirs
        </button>
      </span>
    </div>
  );
}
