import type { BacklogSectionDto } from '@shiplean/core/api/views';
import type { VelocityDto } from '@shiplean/core/api/sprints';
import { backlogTestIds as ids } from '../testIds';
import { SectionToggle } from './SectionToggle';
import type { SectionCollapse } from './useSectionCollapse';
import { useSectionDrop } from './useSectionDrop';
import { VelocityOverride } from './VelocityOverride';

// The Backlog section's header: its items and points, and (with the forecast on) what the forecast plans at.
export function BacklogSectionHeader({
  section,
  velocity,
  forecast,
  projectId,
  projectKey,
  columnCount,
  collapse,
}: {
  section: BacklogSectionDto;
  velocity: VelocityDto | null;
  forecast: boolean;
  projectId: string;
  projectKey: string;
  columnCount: number;
  collapse: SectionCollapse;
}) {
  const { attach, over } = useSectionDrop(section.id);
  return (
    <tr
      ref={attach}
      data-testid={ids.planningSection}
      data-section={section.id}
      data-over={over}
      className={`h-10 border-y border-edge bg-signal-soft ${over ? 'outline-2 -outline-offset-2 outline-signal' : ''}`}
    >
      <td colSpan={columnCount} className="px-2">
        <div className="flex items-center gap-2">
          <SectionToggle
            title={section.title}
            open={collapse.isOpen(section.id)}
            disabled={collapse.searching}
            onToggle={() => collapse.toggle(section.id)}
          />
          <b data-testid={ids.planningSectionTitle} className="text-[length:var(--text-md)]">
            {section.title}
          </b>
          <span
            data-testid={ids.planningSectionCount}
            className="text-[length:var(--text-sm)] text-ink-muted"
          >
            {section.rows.length} items · {section.points} pts
          </span>
          <span className="flex-1" />
          {forecast && velocity ? (
            <VelocityOverride velocity={velocity} projectId={projectId} projectKey={projectKey} />
          ) : null}
        </div>
      </td>
    </tr>
  );
}
