import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import type { ProjectDto } from '../../../api/projects';
import { useTouchShell } from '../../../touch/useTouchShell';
import { workItemTestIds as ids } from '../testIds';
import { fetchHistory, useWorkItem, workItemHistoryKey } from '../workItemApi';
import { RevisionItem } from './RevisionItem';

const RECENT_ON_TOUCH = 3;

// The item's field changes, newest first. The key sits under the item's, so the item's live refetch and Save reach it.
export function HistorySection({ project, itemKey }: { project: ProjectDto; itemKey: string }) {
  const item = useWorkItem(project.id, itemKey).data;
  const history = useQuery({
    queryKey: workItemHistoryKey(project.id, itemKey),
    queryFn: () => fetchHistory(item?.id ?? ''),
    enabled: item !== undefined,
  });
  const { touch } = useTouchShell();
  const [showAll, setShowAll] = useState(false);
  if (!item) return null;
  const revisions = history.data?.revisions ?? [];
  // On a phone the newest three show, then one control for the rest (SHIP-714 D7).
  const truncated = touch && !showAll && revisions.length > RECENT_ON_TOUCH;
  const shown = truncated ? revisions.slice(0, RECENT_ON_TOUCH) : revisions;
  return (
    <section
      data-testid={ids.history}
      aria-label="History"
      className="grid grid-cols-[minmax(0,1fr)] content-start gap-2"
    >
      <div className="flex items-center gap-2">
        <span className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
          History
        </span>
        <span
          data-testid={ids.historyCount}
          className="text-[length:var(--text-xs)] text-ink-muted"
        >
          {revisions.length} revisions
        </span>
      </div>
      <p data-testid={ids.historyHint} className="m-0 text-[length:var(--text-xs)] text-ink-muted">
        Every save is one revision, newest first. Velocity and burndown read State and Story points
        from this trail.
      </p>
      {history.isError ? (
        <p
          data-testid={ids.historyLoadFailed}
          role="alert"
          className="m-0 text-[length:var(--text-sm)] text-alert"
        >
          The history could not be loaded.
        </p>
      ) : (
        <ol className="m-0 grid grid-cols-[minmax(0,1fr)] list-none p-0">
          {shown.map((revision) => (
            <RevisionItem key={revision.rev} revision={revision} />
          ))}
        </ol>
      )}
      {truncated ? (
        <button
          type="button"
          data-testid={ids.historyShowAll}
          className="h-[var(--touch-target)] w-full rounded-control border border-dashed border-edge text-[length:var(--text-sm)] text-signal"
          onClick={() => setShowAll(true)}
        >
          Show all {revisions.length} revisions
        </button>
      ) : null}
    </section>
  );
}
