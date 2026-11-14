import { useNavigate } from 'react-router-dom';
import type { BacklogSectionDto } from '@shiplean/core/api/views';
import { CapacityBar } from '../../../../components/CapacityBar';
import { Chip } from '../../../../components/Chip';
import { routePath } from '../../../../routes';
import { MoreButton } from '../../../../touch/MoreButton';
import type { SheetAction } from '../../../../touch/ActionSheet';
import { useOpenSection } from '../../add/useAddChain';
import { backlogTestIds as ids } from '../../testIds';
import { capacityLoad } from '../capacityLoad';
import { sectionDropId } from '../planningTarget';
import { SectionToggle } from '../SectionToggle';
import type { SectionCollapse } from '../useSectionCollapse';
import type { LitSection } from './useTouchPlanningDrop';

/**
 * A sprint section's header at touch (SHIP-716, D5): the name, Current chip, dates and count on one wrapping line, then
 * a full-width capacity bar with how much is free or over. + Add, Capacity and Open sprint are in its ⋯ (K5). The whole
 * block is a drop target; while a row is lifted over it, it lights with the sum the sprint would come to.
 */
export function TouchSprintBlock({
  section,
  projectKey,
  velocity,
  datesLabel,
  collapse,
  addingOff,
  lit,
}: {
  section: BacklogSectionDto;
  projectKey: string;
  // What the forecast plans at, drawn against a sprint with no capacity entered.
  velocity: number | null;
  datesLabel: string | undefined;
  collapse: SectionCollapse;
  // A filter or search is on: adding is off, because rank is relative to rows you can't see.
  addingOff: boolean;
  lit: LitSection | null;
}) {
  const navigate = useNavigate();
  const { openSection } = useOpenSection();
  const sprint = section.sprint;
  if (sprint === null) return null;
  const current = sprint.status === 'current';
  const { limit, fromVelocity, over, free } = capacityLoad(
    section.points,
    section.capacity,
    velocity,
  );
  const sum = lit?.sum ?? null;
  const used = sum?.after ?? section.points;
  const actions: SheetAction[] = [
    {
      id: 'add',
      label: '+ Add',
      disabled: addingOff,
      onSelect: () => {
        // The add row shows only in an open section.
        collapse.open(section.id);
        openSection(section.id);
      },
    },
    {
      id: current ? 'open-sprint' : 'capacity',
      label: current ? 'Open sprint' : 'Capacity',
      onSelect: () =>
        void navigate(
          routePath(current ? 'sprintBacklog' : 'capacity', {
            key: projectKey,
            sprintId: sprint.id,
          }),
        ),
    },
  ];
  return (
    <div
      data-testid={ids.planningSection}
      data-section={section.id}
      data-drop-target={sectionDropId(section.id)}
      data-over={lit !== null}
      className={`border-y border-edge px-2 py-2 ${lit === null ? 'bg-sunken' : 'bg-accent-soft outline-2 -outline-offset-2 outline-signal'}`}
    >
      <div className="flex items-center gap-1">
        <SectionToggle
          title={section.title}
          open={collapse.isOpen(section.id)}
          disabled={collapse.searching}
          onToggle={() => collapse.toggle(section.id)}
        />
        <div className="flex min-w-0 flex-1 flex-wrap items-baseline gap-x-2 gap-y-0.5">
          <b data-testid={ids.planningSectionTitle} className="text-[length:var(--text-md)]">
            {section.title}
          </b>
          {current ? <Chip tone="accent">Current</Chip> : null}
          {datesLabel ? (
            <span
              data-testid={ids.planningSectionDates}
              className="text-[length:var(--text-sm)] text-ink-muted"
            >
              {datesLabel}
            </span>
          ) : null}
          <span
            data-testid={ids.planningSectionCount}
            className="text-[length:var(--text-sm)] text-ink-muted"
          >
            {section.rows.length} items
          </span>
        </div>
        <MoreButton label={`${section.title} actions`} actions={actions} />
      </div>
      <div className="flex items-center gap-2 pl-[var(--touch-target)] text-[length:var(--text-sm)]">
        {limit === null ? null : (
          <span className="min-w-0 flex-1">
            <CapacityBar
              used={used}
              capacity={limit}
              variant={fromVelocity ? 'velocity' : 'capacity'}
            />
          </span>
        )}
        {sum === null ? (
          <span data-testid={ids.planningCommitted} className="whitespace-nowrap">
            <b>{section.points}</b>
            {limit === null ? '' : ` / ${limit}`} pts
          </span>
        ) : (
          <span
            data-testid={ids.sectionPreview}
            data-over={sum.over}
            className={`font-semibold whitespace-nowrap ${sum.over ? 'text-alert' : 'text-accent'}`}
          >
            {sum.text} pts
          </span>
        )}
        {fromVelocity && limit !== null ? (
          <span data-testid={ids.planningVelocityChip} title="Nobody has entered capacity yet">
            <Chip tone="warn">velocity</Chip>
          </span>
        ) : null}
        {lit !== null || limit === null ? null : over === null ? (
          <span data-testid={ids.planningFree} className="whitespace-nowrap text-ink-muted">
            {free} free
          </span>
        ) : (
          <span
            data-testid={ids.planningOver}
            className="font-semibold whitespace-nowrap text-alert"
          >
            +{over} over
          </span>
        )}
      </div>
      {lit === null ? null : (
        <p
          data-testid={ids.planningDropMessage}
          className="pl-[var(--touch-target)] pt-1 text-[length:var(--text-sm)] font-semibold text-accent"
        >
          {lit.message}
        </p>
      )}
    </div>
  );
}
