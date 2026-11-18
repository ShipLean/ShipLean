import { TypeIcon } from '../../../../components/TypeIcon';
import type { RetroCoverageRowDto } from '@shiplean/core/api/retros';
import { ItemLink } from '../../../itemPeek/ItemLink';
import { retrosTestIds as ids } from '../../testIds';
import { RailSection, sheetRowClass } from './RailSection';

function CoverageMark({ status }: { status: RetroCoverageRowDto['status'] }) {
  if (status === 'done')
    return (
      <span className="font-bold text-good" title="Retro logged">
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
    <span className="text-[length:var(--text-xs)] font-semibold text-alert" title="No retro">
      missing
    </span>
  );
}

/** Coverage: the finished stories, bugs and Action items and the Features with a child that finished or rolled over, each with a ✓, "missing" or "waiting". */
export function CoverageSection({
  rows,
  sheet = false,
}: {
  rows: RetroCoverageRowDto[];
  sheet?: boolean;
}) {
  return (
    <RailSection
      testId={ids.retroCoverage}
      title="Coverage"
      hint="Stories, bugs and Action items finished in the sprint, and Features with a child that finished or rolled over, and whether a retro was logged."
      empty={rows.length === 0}
      sheet={sheet}
      note={
        sheet && rows.some((r) => r.status === 'waiting')
          ? 'Waiting: the retro is logged when its last story closes.'
          : undefined
      }
    >
      {rows.map((row) => (
        <div
          key={row.item.id}
          data-testid={ids.retroCoverageRow}
          data-item-key={row.item.key}
          data-status={row.status}
          className={`flex items-center gap-1.5 ${sheet ? `gap-2 ${sheetRowClass}` : ''}`}
        >
          <TypeIcon type={row.item.type.id} />
          <ItemLink
            itemKey={row.item.key}
            title={row.item.title}
            className={`min-w-0 truncate text-ink no-underline ${sheet ? 'inline-flex min-h-8 items-center' : ''}`}
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
