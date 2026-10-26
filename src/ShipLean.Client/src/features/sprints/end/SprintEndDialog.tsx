import type { RolloverDto, SprintMetricsDto } from '../../../api/sprints';
import { Dialog } from '../../../components/Dialog';
import { sprintsTestIds as ids } from '../testIds';
import { NextSprintCallout } from './NextSprintCallout';
import {
  destinationOf,
  nextSprintFigures,
  pointsMovingToNext,
  setAll,
  type Choices,
  type Destination,
} from './rolloverChoices';
import { UnfinishedRow } from './UnfinishedRow';

interface SprintEndDialogProps {
  rollover: RolloverDto;
  /** The ended sprint's committed and completed points, once they have loaded. */
  metrics: SprintMetricsDto | undefined;
  choices: Choices;
  onChoices: (choices: Choices) => void;
  onApply: () => void;
  onLater: () => void;
  applying: boolean;
  error: string | null;
}

/** The Sprint end dialog: each unfinished item with where it goes, the next sprint's total, Apply and Decide later. */
export function SprintEndDialog({
  rollover,
  metrics,
  choices,
  onChoices,
  onApply,
  onLater,
  applying,
  error,
}: SprintEndDialogProps) {
  const { sprint, items, nextSprint } = rollover;
  const points = items.reduce((sum, item) => sum + (item.points ?? 0), 0);
  const choose = (itemId: string, destination: Destination) =>
    onChoices(new Map(choices).set(itemId, destination));
  const everyChoice = (destination: Destination) => onChoices(setAll(items, destination));
  return (
    <Dialog
      title={`${sprint.name} has ended`}
      onClose={onLater}
      width="720px"
      footer={
        <>
          <span className="text-[length:var(--text-xs)] text-ink-muted">
            Anyone on the team can do this. Once applied, it&apos;s done for everyone.
          </span>
          <span className="flex-1" />
          <button type="button" data-testid={ids.endLater} className="btn-ghost" onClick={onLater}>
            Decide later
          </button>
          <button
            type="button"
            data-testid={ids.endApply}
            className="btn-signal"
            disabled={applying}
            onClick={onApply}
          >
            Apply
          </button>
        </>
      }
    >
      <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
        {`${metrics ? `${metrics.completed} of ${metrics.committed} committed points completed · ` : ''}${items.length} ${items.length === 1 ? 'item' : 'items'} unfinished (${points} pts)`}
      </p>
      <div>
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
      <div className="flex flex-wrap items-center gap-2">
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
      <div
        data-testid={ids.endHistory}
        className="rounded-panel border border-edge border-l-[3px] border-l-edge bg-sunken px-3 py-2 text-[length:var(--text-sm)]"
      >
        <b className="block">History stays as it was</b>
        {`${sprint.name} keeps ${metrics ? `${metrics.committed} committed / ${metrics.completed} completed` : 'its committed and completed points'} for velocity, because both come from revisions at its end date. Open tasks move with their story; closed tasks stay in ${sprint.name}.`}
      </div>
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
