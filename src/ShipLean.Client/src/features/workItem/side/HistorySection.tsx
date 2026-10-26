import { useQuery } from '@tanstack/react-query';
import type { ProjectDto } from '../../../api/projects';
import { workItemTestIds as ids } from '../testIds';
import { fetchHistory, useWorkItem, workItemHistoryKey } from '../workItemApi';
import { RevisionItem } from './RevisionItem';

// The item's field changes, newest first. The key sits under the item's, so the item's live refetch and Save reach it.
export function HistorySection({ project, itemKey }: { project: ProjectDto; itemKey: string }) {
  const item = useWorkItem(project.id, itemKey).data;
  const history = useQuery({
    queryKey: workItemHistoryKey(project.id, itemKey),
    queryFn: () => fetchHistory(item?.id ?? ''),
    enabled: item !== undefined,
  });
  if (!item) return null;
  const revisions = history.data?.revisions ?? [];
  return (
    <section data-testid={ids.history} aria-label="History" className="grid content-start gap-2">
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
        <ol className="m-0 grid list-none p-0">
          {revisions.map((revision) => (
            <RevisionItem key={revision.rev} revision={revision} />
          ))}
        </ol>
      )}
    </section>
  );
}
