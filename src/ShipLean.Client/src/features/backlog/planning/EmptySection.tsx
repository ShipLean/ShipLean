import type { BacklogSectionDto } from '../../../api/views';
import { backlogTestIds as ids } from '../testIds';
import { useSectionDrop } from './useSectionDrop';

// What a sprint with no rows shows, and a place to drop a row to put it in the sprint.
export function EmptySection({
  section,
  columnCount,
}: {
  section: BacklogSectionDto;
  columnCount: number;
}) {
  const { attach, over } = useSectionDrop(section.id, 'hint');
  return (
    <tr ref={attach}>
      <td
        colSpan={columnCount}
        data-testid={ids.planningEmptySection}
        data-over={over}
        className={`h-[52px] text-center text-[length:var(--text-sm)] text-ink-muted ${over ? 'bg-signal-soft text-ink' : ''}`}
      >
        No stories or bugs planned in {section.title}
      </td>
    </tr>
  );
}
