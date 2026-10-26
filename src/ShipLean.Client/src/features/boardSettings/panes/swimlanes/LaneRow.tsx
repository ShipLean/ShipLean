import type { KeyboardEvent } from 'react';
import { CriteriaBuilder } from '../../../../criteria/CriteriaBuilder';
import { newClause } from '../../../../criteria/defaultClause';
import type { PickLists } from '../../../../criteria/valueOptions';
import { SWATCH_NAMES } from '../swatchNames';
import { SWATCHES, type Swatch, type SwimlaneJson } from '../../../../api/persisted';
import { boardSettingsTestIds as ids } from '../../testIds';
import { acceptsDrops, withColour, withCriteria } from './laneEdits';

const iconButton =
  'inline-grid size-6 place-items-center rounded-control text-ink-muted hover:bg-sunken disabled:cursor-default disabled:opacity-35';
const modeButton =
  'px-2.5 py-0.5 text-[length:var(--text-sm)] aria-pressed:bg-signal-soft aria-pressed:font-medium aria-pressed:text-ink';

/** One lane: name, colour, how cards get in (criteria or by hand), and the criteria. The default lane takes the rest. */
export function LaneRow({
  lane,
  lists,
  readOnly,
  onChange,
  onDelete,
  onMove,
  onDragStart,
  onDrop,
}: {
  lane: SwimlaneJson;
  lists: PickLists;
  readOnly: boolean;
  onChange(next: SwimlaneJson): void;
  onDelete(): void;
  /** -1 up, 1 down. */
  onMove(step: number): void;
  onDragStart(): void;
  onDrop(): void;
}) {
  const isDefault = lane.kind === 'default';
  const keyDown = (event: KeyboardEvent) => {
    if (!event.altKey) return;
    if (event.key === 'ArrowUp') onMove(-1);
    else if (event.key === 'ArrowDown') onMove(1);
    else return;
    event.preventDefault();
  };
  const toCriteria = () =>
    onChange({
      ...lane,
      kind: 'criteria',
      criteria: { match: 'all', clauses: [newClause('priority', lists)] },
    });
  return (
    <div
      data-testid={ids.laneRow}
      className="grid gap-2 rounded-panel border border-edge bg-panel p-2.5"
      onDragOver={(event) => event.preventDefault()}
      onDrop={onDrop}
    >
      <div className="flex items-center gap-2">
        <button
          type="button"
          data-testid={ids.laneGrip}
          draggable={!readOnly}
          disabled={readOnly}
          aria-label={`Move lane ${lane.name} (Alt+Up or Alt+Down)`}
          title="Drag, or press Alt+Up or Alt+Down"
          className={`${iconButton} cursor-grab`}
          onKeyDown={keyDown}
          onDragStart={onDragStart}
        >
          ⠿
        </button>
        <input
          data-testid={ids.laneName}
          className="field h-[26px] min-w-20 flex-1"
          aria-label="Lane name"
          value={lane.name}
          readOnly={readOnly}
          onChange={(event) => onChange({ ...lane, name: event.target.value })}
        />
        <select
          data-testid={ids.laneColour}
          className="field h-[26px]"
          aria-label={`Colour of ${lane.name}`}
          value={lane.colour ?? ''}
          disabled={readOnly}
          style={{
            borderLeft: `4px solid ${lane.colour ? `var(--color-${lane.colour})` : 'transparent'}`,
          }}
          onChange={(event) =>
            onChange(withColour(lane, (event.target.value || null) as Swatch | null))
          }
        >
          <option value="">No colour</option>
          {SWATCHES.map((swatch) => (
            <option key={swatch} value={swatch}>
              {SWATCH_NAMES[swatch]}
            </option>
          ))}
        </select>
        {isDefault ? (
          <span
            data-testid={ids.laneDefaultChip}
            className="rounded-control bg-sunken px-2 py-0.5 text-[length:var(--text-xs)]"
            title="Takes every card no other lane takes"
          >
            Default lane
          </span>
        ) : (
          <div
            role="group"
            aria-label={`How cards get into ${lane.name}`}
            className="inline-flex overflow-hidden rounded-control border border-edge"
          >
            <button
              type="button"
              data-testid={ids.laneModeCriteria}
              aria-pressed={lane.kind === 'criteria'}
              disabled={readOnly}
              className={modeButton}
              onClick={toCriteria}
            >
              Criteria
            </button>
            <button
              type="button"
              data-testid={ids.laneModeManual}
              aria-pressed={lane.kind === 'manual'}
              disabled={readOnly}
              className={modeButton}
              onClick={() => onChange({ ...lane, kind: 'manual', criteria: null })}
            >
              Manual
            </button>
          </div>
        )}
        <button
          type="button"
          data-testid={ids.laneDelete}
          disabled={readOnly}
          aria-label={`Delete lane ${lane.name}`}
          title={isDefault ? "The default lane can't be deleted" : 'Delete lane'}
          className={`${iconButton} ${isDefault ? 'opacity-35' : ''}`}
          onClick={onDelete}
        >
          ✕
        </button>
      </div>
      {isDefault ? (
        <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
          Every card that no other lane takes. It can be renamed and moved, but not deleted.
        </p>
      ) : lane.kind === 'manual' ? (
        <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
          Holds the cards someone drags into it.
        </p>
      ) : (
        <div data-testid={ids.laneCriteria}>
          <CriteriaBuilder
            value={lane.criteria ?? { match: 'all', clauses: [] }}
            onChange={(criteria) => onChange(withCriteria(lane, criteria))}
            {...lists}
          />
        </div>
      )}
      {acceptsDrops(lane) ? null : (
        <p data-testid={ids.laneNoDrops} className="m-0 text-[length:var(--text-xs)] text-caution">
          Cards can't be dropped into this lane: it has no criteria, or they aren't all "=" on
          priority, severity, points, assignee or sprint, so the board can't say what to set. Cards
          that match still land here, and a matching card can't be dragged out of it to another
          lane.
        </p>
      )}
    </div>
  );
}
