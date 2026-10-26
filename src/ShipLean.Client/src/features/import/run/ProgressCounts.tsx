import type { ImportCounts } from '../../../api/persisted';
import { importTestIds as ids } from '../testIds';

const labels: Record<keyof ImportCounts, string> = {
  sprints: 'Sprints',
  issues: 'Issues',
  comments: 'Comments',
  attachments: 'Attachments',
  revisions: 'Revisions',
};
const entities = Object.keys(labels) as (keyof ImportCounts)[];
const n = (value: number) => value.toLocaleString('en-GB');

export function ProgressCounts({ counts }: { counts: ImportCounts }) {
  return (
    <dl
      data-testid={ids.runCounts}
      className="m-0 grid grid-cols-[auto_1fr] gap-x-[var(--space-4)]"
    >
      {entities.map((entity) => {
        const { done, total, failed } = counts[entity];
        return (
          <div key={entity} data-testid={ids.runCount} data-entity={entity} className="contents">
            <dt className="text-ink-muted">{labels[entity]}</dt>
            <dd className="m-0 font-mono">
              {n(done)} / {n(total)}
              {failed > 0 ? <span className="text-alert"> · {n(failed)} failed</span> : null}
            </dd>
          </div>
        );
      })}
    </dl>
  );
}
