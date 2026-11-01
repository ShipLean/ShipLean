import { TypeIcon } from '../../../../components/TypeIcon';
import type { RetroCoverageRowDto } from '../../../../api/retros';
import { ItemLink } from '../../../itemPeek/ItemLink';
import { retrosTestIds as ids } from '../../testIds';
import { RailSection } from './RailSection';

function CoverageMark({ status }: { status: RetroCoverageRowDto['status'] }) {
  if (status === 'done')
    return (
      <span className="font-bold text-ok" title="Retro logged">
        ✓
      </span>
    );
  if (status === 'waiting')
    return (
      <span
        className="text-[length:var(--text-xs)] text-ink-muted"
        title="Logged when its last story closes"
      >
        waiting
      </span>
    );
  return (
    <span className="text-[length:var(--text-xs)] font-semibold text-danger" title="No retro">
      missing
    </span>
  );
}

/** Coverage: the finished stories, bugs and features, each with a ✓, "missing" or "waiting". */
export function CoverageSection({ rows }: { rows: RetroCoverageRowDto[] }) {
  return (
    <RailSection
      testId={ids.retroCoverage}
      title="Coverage"
      hint="Stories, bugs and features finished in the sprint, and whether a retro was logged."
      empty={rows.length === 0}
    >
      {rows.map((row) => (
        <div
          key={row.item.id}
          data-testid={ids.retroCoverageRow}
          data-item-key={row.item.key}
          data-status={row.status}
          className="flex items-center gap-1.5"
        >
          <TypeIcon type={row.item.type.id} />
          <ItemLink
            itemKey={row.item.key}
            title={row.item.title}
            className="min-w-0 truncate text-ink no-underline"
          >
            {row.item.title}
          </ItemLink>
          <span className="flex-1" />
          <CoverageMark status={row.status} />
        </div>
      ))}
    </RailSection>
  );
}
