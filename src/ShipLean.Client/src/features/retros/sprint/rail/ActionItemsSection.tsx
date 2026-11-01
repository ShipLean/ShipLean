import { StateDot } from '../../../../components/StateDot';
import { TypeIcon } from '../../../../components/TypeIcon';
import type { RetroActionRowDto } from '../../../../api/retros';
import { ItemLink } from '../../../itemPeek/ItemLink';
import { retrosTestIds as ids } from '../../testIds';
import { RailSection } from './RailSection';

/** Action items: each with its key (opens the item), state, the sprint it is planned in or "Not planned", where it was raised. */
export function ActionItemsSection({ rows }: { rows: RetroActionRowDto[] }) {
  return (
    <RailSection
      testId={ids.retroRailActions}
      title="Action items"
      hint="Action items raised from this sprint's points, plus earlier ones still open, and where they are planned."
      empty={rows.length === 0}
    >
      {rows.map(({ action, raisedIn }) => (
        <div
          key={action.workItemId}
          data-item-key={action.key}
          className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-0.5 border-b border-edge/60 pb-1.5"
        >
          <div className="flex items-center gap-1.5">
            <TypeIcon type="action" />
            <span className="min-w-0 truncate" title={action.title}>
              {action.title}
            </span>
          </div>
          <div className="flex flex-wrap items-center gap-1.5 text-[length:var(--text-xs)] text-ink-muted">
            <ItemLink
              itemKey={action.key}
              title={action.title}
              className="font-mono text-ink-muted no-underline"
            >
              {action.key}
            </ItemLink>
            <StateDot category={action.state.category} name={action.state.name} />
            <span data-planned="">· {action.sprint?.name ?? 'Not planned'}</span>
            {raisedIn ? <span>· from {raisedIn.name}</span> : null}
          </div>
        </div>
      ))}
    </RailSection>
  );
}
