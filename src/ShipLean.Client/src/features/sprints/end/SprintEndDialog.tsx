import type { RolloverDto, SprintMetricsDto } from '@shiplean/core/api/sprints';
import { Dialog } from '../../../components/Dialog';
import { useTouchShell } from '../../../touch/useTouchShell';
import { sprintsTestIds as ids } from '../testIds';
import { EndsEarlyNote } from './EndsEarlyNote';
import { NextSprintCallout } from './NextSprintCallout';
import {
  destinationOf,
  nextSprintFigures,
  pointsMovingToNext,
  setAll,
  type Choices,
  type Destination,
} from './rolloverChoices';
import { DestinationOptions, UnfinishedRow } from './UnfinishedRow';

interface SprintEndDialogProps {
  rollover: RolloverDto;
  /** The ended sprint's committed and completed points, once they have loaded. */
  metrics: SprintMetricsDto | undefined;
  choices: Choices;
  onChoices: (choices: Choices) => void;
  onApply: () => void;
  onCancel: () => void;
  applying: boolean;
  error: string | null;
}

/**
 * The Close sprint dialog: each unfinished item with where it goes, the next sprint's total, Close sprint and Cancel. At
 * 900px and below (SHIP-724 D9 design) Set all to is a select above the item cards, the history note is one plain line,
 * and the button names the sprint.
 */
export function SprintEndDialog({
  rollover,
  metrics,
  choices,
  onChoices,
  onApply,
  onCancel,
  applying,
  error,
}: SprintEndDialogProps) {
  const { sprint, items, nextSprint } = rollover;
  const points = items.reduce((sum, item) => sum + (item.points ?? 0), 0);
  const choose = (itemId: string, destination: Destination) =>
    onChoices(new Map(choices).set(itemId, destination));
  const everyChoice = (destination: Destination) => onChoices(setAll(items, destination));
  const { touch } = useTouchShell();
  const destinations = new Set(items.map((item) => destinationOf(item, choices)));
  // The select shows the one destination every item has; mixed choices show a placeholder until one is picked.
  const common = destinations.size === 1 ? [...destinations][0] : '';
  const unfinished = `${items.length} ${items.length === 1 ? 'item' : 'items'} unfinished (${points} pts)`;
  return (
    <Dialog
      title={`Close ${sprint.name}`}
      onClose={onCancel}
      width="720px"
      footer={
        <>
          <span className="text-[length:var(--text-xs)] text-ink-muted touch:hidden">
            Anyone on the team can do this. Once closed, it&apos;s done for everyone.
          </span>
          <span className="flex-1 touch:hidden" />
          <button
            type="button"
            data-testid={ids.endCancel}
            className="btn-ghost touch:hidden"
            onClick={onCancel}
          >
            Cancel
          </button>
          <button
            type="button"
            data-testid={ids.endApply}
            className="btn-signal touch:h-[var(--touch-target)] touch:flex-1"
            disabled={applying}
            onClick={onApply}
          >
            {touch ? `Close ${sprint.name}` : 'Close sprint'}
          </button>
        </>
      }
    >
      {touch ? (
        <p data-testid={ids.endUnfinished} className="m-0 font-semibold text-ink">
          {unfinished}
        </p>
      ) : (
        <p
          data-testid={ids.endUnfinished}
          className="m-0 text-[length:var(--text-sm)] text-ink-muted"
        >
          {`${metrics ? `${metrics.completed} of ${metrics.committed} committed points completed · ` : ''}${unfinished}`}
        </p>
      )}
      {rollover.endsEarly ? <EndsEarlyNote sprintName={sprint.name} /> : null}
      {touch ? (
        <div className="grid gap-1">
          <label
            htmlFor="sprint-end-set-all"
            className="text-[length:var(--text-sm)] text-ink-muted"
          >
            Set all to
          </label>
          <select
            id="sprint-end-set-all"
            data-testid={ids.endSetAllSelect}
            className="field w-full"
            value={common}
            onChange={(event) => everyChoice(event.target.value as Destination)}
          >
            {common === '' ? (
              <option value="" disabled>
                Mixed
              </option>
            ) : null}
            <DestinationOptions sprint={sprint} nextSprint={nextSprint} />
          </select>
        </div>
      ) : null}
      <div className={touch ? 'grid gap-3' : undefined}>
        {items.map((item) => (
          <UnfinishedRow
            key={item.workItemId}
            item={item}
            sprint={sprint}
            nextSprint={nextSprint}
            destination={destinationOf(item, choices)}
            onChange={(destination) => choose(item.workItemId, destination)}
          />
        ))}
      </div>
      <div className="flex flex-wrap items-center gap-2 touch:hidden">
        <span className="text-[length:var(--text-sm)] text-ink-muted">Set all to</span>
        {nextSprint ? (
          <button
            type="button"
            data-testid={ids.endSetAll}
            className="btn-ghost"
            onClick={() => everyChoice('next')}
          >
            {nextSprint.name}
          </button>
        ) : null}
        <button
          type="button"
          data-testid={ids.endSetAll}
          className="btn-ghost"
          onClick={() => everyChoice('backlog')}
        >
          Top of backlog
        </button>
        <button
          type="button"
          data-testid={ids.endSetAll}
          className="btn-ghost"
          onClick={() => everyChoice('stay')}
        >
          {`Leave in ${sprint.name}`}
        </button>
      </div>
      {nextSprint ? (
        <NextSprintCallout
          sprint={nextSprint}
          figures={nextSprintFigures(
            rollover.nextSprintPoints,
            rollover.nextSprintCapacity,
            pointsMovingToNext(items, choices),
          )}
        />
      ) : null}
      {touch ? (
        <p data-testid={ids.endHistory} className="m-0 text-[length:var(--text-sm)] text-ink-muted">
          {`${sprint.name} keeps ${metrics ? `${metrics.committed} committed and ${metrics.completed} completed` : 'its committed and completed points'} for velocity. Open tasks move with their story; closed tasks stay in ${sprint.name}.`}
        </p>
      ) : (
        <div
          data-testid={ids.endHistory}
          className="rounded-panel border border-edge border-l-[3px] border-l-edge bg-sunken px-3 py-2 text-[length:var(--text-sm)]"
        >
          <b className="block">History stays as it was</b>
          {`${sprint.name} keeps ${metrics ? `${metrics.committed} committed / ${metrics.completed} completed` : 'its committed and completed points'} for velocity. Committed is what was in it at 00:00 UTC on its start date; completed is what finished in it and was still finished when it ended. Open tasks move with their story; closed tasks stay in ${sprint.name}.`}
        </div>
      )}
      <p
        data-testid={ids.endError}
        role="alert"
        hidden={error === null}
        className="m-0 text-[length:var(--text-sm)] text-alert"
      >
        {error}
      </p>
    </Dialog>
  );
}
