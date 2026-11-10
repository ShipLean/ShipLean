import { useQuery } from '@tanstack/react-query';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { AskHistoryLineDto } from '@shiplean/core/api/asks';
import type { RevisionDto } from '@shiplean/core/api/workItems';
import { AskHistoryLine } from '../../asks/AskHistoryLine';
import { workItemTestIds as ids } from '../testIds';
import { fetchHistory, useWorkItem, workItemHistoryKey } from '../workItemApi';
import { RevisionItem } from './RevisionItem';

type RailEntry =
  | { kind: 'revision'; at: string; revision: RevisionDto }
  | { kind: 'ask'; at: string; line: AskHistoryLineDto };

// Both lists come newest first with server UTC ISO times, so the strings compare as times. A revision wins a tie.
function interleave(revisions: RevisionDto[], asks: AskHistoryLineDto[]): RailEntry[] {
  const entries: RailEntry[] = [
    ...revisions.map((revision): RailEntry => ({
      kind: 'revision',
      at: revision.changedAt,
      revision,
    })),
    ...asks.map((line): RailEntry => ({ kind: 'ask', at: line.at, line })),
  ];
  return entries
    .map((entry, index) => ({ entry, index }))
    .sort((a, b) =>
      a.entry.at === b.entry.at ? a.index - b.index : a.entry.at < b.entry.at ? 1 : -1,
    )
    .map(({ entry }) => entry);
}

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
  const rail = interleave(revisions, history.data?.asks ?? []);
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
        from this trail. Decisions and questions aren't fields, so they don't add a revision. Each
        one raised, answered, changed or withdrawn is still a line here, with its D or Q badge.
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
          {rail.map((entry) =>
            entry.kind === 'revision' ? (
              <RevisionItem key={`rev-${entry.revision.rev}`} revision={entry.revision} />
            ) : (
              <AskHistoryLine
                key={`${entry.line.badge}-${entry.line.kind}-${entry.line.at}`}
                line={entry.line}
              />
            ),
          )}
        </ol>
      )}
    </section>
  );
}
