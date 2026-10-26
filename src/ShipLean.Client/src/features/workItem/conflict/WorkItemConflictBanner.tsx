import { FIELD_IDS } from '../../../api/persisted';
import type { ConflictDto } from '../../../api/workItems';
import { workItemTestIds as ids } from '../testIds';
import { fieldLabels, type DraftField } from '../form/draft';

const labelOf = (field: string): string =>
  FIELD_IDS.find((definition) => definition.id === field)?.label ??
  fieldLabels[field as DraftField] ??
  field;

const buttonClass =
  'inline-flex h-7 items-center rounded-control border px-3 text-[length:var(--text-sm)] font-medium focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-signal';

// Shown when a save finds the item at a newer revision: who changed what since the revision the draft was edited from,
// what the user has not saved, and the two ways out.
export function WorkItemConflictBanner({
  conflict,
  baseRevision,
  mine,
  onReloadTheirs,
  onReapplyMine,
}: {
  conflict: ConflictDto;
  baseRevision: number;
  mine: readonly DraftField[];
  onReloadTheirs: () => void;
  onReapplyMine: () => void;
}) {
  const { current, theirChanges } = conflict;
  const whoOf = (revision: ConflictDto['theirChanges'][number]) =>
    revision.agentName ?? revision.changedBy?.name ?? 'Jira import';
  const newest = theirChanges[0];
  return (
    <div
      data-testid={ids.conflict}
      role="alert"
      className="grid gap-2 border-b border-edge bg-sunken px-4 py-2 text-[length:var(--text-sm)] text-ink"
      style={{ borderLeft: '3px solid var(--color-warn)' }}
    >
      <div data-testid={ids.conflictSummary}>
        <b>
          {newest ? whoOf(newest) : 'Someone'} saved {current.key} first, so your save didn&apos;t
          go through.
        </b>{' '}
        <span className="text-ink-muted">
          You were editing revision {baseRevision}; it&apos;s now at revision {current.revision}.
          Nothing you typed is lost.
        </span>
      </div>
      <div className="grid grid-cols-2 gap-4">
        <div className="grid content-start gap-1">
          <span className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
            Changed by them
          </span>
          <ul data-testid={ids.conflictTheirs} className="grid gap-1">
            {theirChanges.flatMap((revision) =>
              revision.changes.map((change) => (
                <li
                  key={`${revision.rev}-${change.field}`}
                  data-testid={ids.conflictTheirChange}
                  data-field={change.field}
                >
                  <b>{labelOf(change.field)}</b>
                  {change.toLabel !== null ? (
                    <>
                      {' '}
                      {change.fromLabel ?? 'none'} → {change.toLabel}
                    </>
                  ) : null}{' '}
                  <span className="text-ink-muted">
                    by {whoOf(revision)}, revision {revision.rev}
                  </span>
                  {mine.includes(change.field as DraftField) ? (
                    <span
                      data-testid={ids.conflictAlsoMine}
                      className="ml-2 rounded-control border border-edge px-1 text-[length:var(--text-xs)] text-caution"
                    >
                      you changed this too
                    </span>
                  ) : null}
                </li>
              )),
            )}
          </ul>
        </div>
        <div className="grid content-start gap-1">
          <span className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
            Your unsaved changes
          </span>
          <ul data-testid={ids.conflictMine} className="grid gap-1">
            {mine.map((field) => (
              <li key={field}>{fieldLabels[field]}</li>
            ))}
          </ul>
        </div>
      </div>
      <div className="flex items-center gap-2">
        <button
          type="button"
          data-testid={ids.conflictReloadTheirs}
          className={`${buttonClass} border-edge bg-panel text-ink hover:bg-signal-soft`}
          onClick={onReloadTheirs}
        >
          Reload theirs
        </button>
        <button
          type="button"
          data-testid={ids.conflictReapplyMine}
          className={`${buttonClass} border-signal bg-signal text-signal-ink`}
          onClick={onReapplyMine}
        >
          Re-apply mine
        </button>
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          Re-apply puts your changes on top of revision {current.revision}, and yours win where you
          both changed a field. Then Save.
        </span>
      </div>
    </div>
  );
}
