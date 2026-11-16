import type { CSSProperties, ReactNode } from 'react';
import { Link } from 'react-router-dom';
import type {
  InspectionEventDto,
  InspectionEventKind,
  InspectionSegmentDto,
  InspectionSegmentKind,
  InspectionTaskMeasurementDto,
} from '@shiplean/core/api/views';
import { TypeIcon } from '../../../components/TypeIcon';
import { routePath } from '../../../routes';
import { SPLIT_COLOURS } from '../leadtime/SplitBar';
import { OVERLINE } from '../overview/overviewStyles';
import { inspectionMetricsTestIds as ids } from '../testIds';

const TAIL = 'repeating-linear-gradient(135deg, var(--color-muted) 0 2px, transparent 2px 4px)';

const SEGMENT: Record<InspectionSegmentKind, { name: string; background: string }> = {
  active: { name: 'Active', background: SPLIT_COLOURS.active },
  blocked: { name: 'Blocked', background: SPLIT_COLOURS.blocked },
  rework: { name: 'Rework', background: SPLIT_COLOURS.rework },
  unresolvedTail: { name: 'Commit to Resolved', background: TAIL },
};

const MARK: Record<InspectionEventKind | 'end', CSSProperties> = {
  started: { background: SPLIT_COLOURS.active },
  commit: { background: 'var(--color-surface)', border: `2px solid ${SPLIT_COLOURS.active}` },
  resolved: { background: 'var(--state-resolved)' },
  blocked: { background: SPLIT_COLOURS.blocked },
  unblocked: { background: 'var(--color-muted)' },
  reviewApproved: { background: SPLIT_COLOURS.rework },
  reviewChangesRequested: { background: SPLIT_COLOURS.rework },
  end: { background: 'var(--color-text)', borderRadius: 2 },
};

/** The segments laid out on one track from the start to the last segment's end; the tail is hatched. */
function Track({ segments }: { segments: readonly InspectionSegmentDto[] }) {
  const span = Math.max(0, ...segments.map((s) => s.fromHours + s.hours));
  if (span <= 0) return null;
  return (
    <div className="relative h-2.5 overflow-hidden rounded-sm bg-sunken">
      {segments.map((segment) => (
        <i
          key={`${segment.kind}-${segment.from}`}
          data-testid={ids.railSegment}
          data-kind={segment.kind}
          data-counted={segment.counted}
          className="absolute inset-y-0 block"
          style={{
            left: `${(segment.fromHours / span) * 100}%`,
            width: `${(segment.hours / span) * 100}%`,
            background: SEGMENT[segment.kind].background,
          }}
        />
      ))}
    </div>
  );
}

interface Row {
  key: string;
  kind: InspectionEventKind | 'end';
  counted: boolean;
  body: ReactNode;
  atLabel: string;
}

function eventBody(event: InspectionEventDto, counted: boolean) {
  const what =
    event.kind === 'commit' ? (
      <>
        <b>Commit</b> <code className="font-mono text-[length:var(--text-xs)]">{event.text}</code>
      </>
    ) : event.kind === 'started' ? (
      <>
        <b>Started</b>: first move to {event.text}
      </>
    ) : (
      <b className={counted ? undefined : 'font-normal'}>{event.text}</b>
    );
  return counted ? (
    what
  ) : (
    <>
      {what} · <span className="text-ink-muted">after the end, not counted</span>
    </>
  );
}

/** The events in the server's order, with the lead time's end placed after the last event at or before it. */
function rowsOf({ task, events }: InspectionTaskMeasurementDto): Row[] {
  const ended = Date.parse(task.endedAt);
  const end: Row = {
    key: 'end',
    kind: 'end',
    counted: true,
    atLabel: task.endedLabel,
    body:
      task.endedBy === 'lastCommit' ? (
        <>
          <b>Lead time ends</b> at the last commit: <b>{task.leadLabel}</b>
        </>
      ) : (
        <>
          <b>Lead time ends</b> at Resolved: <b>{task.leadLabel}</b> ·{' '}
          <span className="text-ink-muted">no commit time was reported</span>
        </>
      ),
  };
  const rows: Row[] = [];
  let placed = false;
  events.forEach((event, i) => {
    const counted = Date.parse(event.at) <= ended;
    if (!counted && !placed) {
      rows.push(end);
      placed = true;
    }
    rows.push({
      key: `${event.kind}-${i}`,
      kind: event.kind,
      counted,
      atLabel: event.atLabel,
      body: eventBody(event, counted),
    });
  });
  if (!placed) rows.push(end);
  return rows;
}

/** The selected task's measurement: its parts on one track, how it was measured, and links to the task and its timeline. */
export function PeriodRail({
  projectKey,
  selected,
  noTasks,
  onOpenTask,
}: {
  projectKey: string;
  selected: InspectionTaskMeasurementDto | null;
  /** No task ended in the sprint, so there is nothing to select. */
  noTasks: boolean;
  onOpenTask(): void;
}) {
  if (selected === null)
    return (
      <aside
        data-testid={ids.railEmpty}
        className="panel p-4 text-[length:var(--text-sm)] text-ink-muted"
      >
        {noTasks
          ? 'Nothing to measure yet'
          : 'Select a task to see how its lead time was measured.'}
      </aside>
    );
  const { task, segments } = selected;
  return (
    <aside
      data-testid={ids.rail}
      data-key={task.key}
      className="panel grid content-start gap-4 p-4"
    >
      <section className="grid gap-1">
        <div className="flex items-center gap-1.5 text-[length:var(--text-sm)]">
          <TypeIcon type="task" />
          <span className="font-mono text-[length:var(--text-xs)] text-ink-muted">{task.key}</span>
          <span>
            {task.who}
            {task.agent && <span className="text-ink-muted"> (agent)</span>}
          </span>
        </div>
        <h3 className="m-0 text-[length:var(--text-md)] font-semibold">{task.title}</h3>
        <div className="text-[length:var(--text-sm)] text-ink-muted">
          {task.featureTitle ?? 'No Feature'}
          {task.parentKey !== null && task.parentType !== null && (
            <>
              {' · under '}
              <TypeIcon type={task.parentType} />{' '}
              <span className="font-mono text-[length:var(--text-xs)]">{task.parentKey}</span>
            </>
          )}
        </div>
      </section>
      <section className="grid gap-2">
        <div className={OVERLINE}>Lead time</div>
        <div className="text-[24px] font-bold">{task.leadLabel}</div>
        <Track segments={segments} />
        <ul className="m-0 grid list-none gap-1 p-0 text-[length:var(--text-sm)]">
          {segments.map((segment) => (
            <li
              key={`${segment.kind}-${segment.from}`}
              className="grid grid-cols-[10px_1fr_auto] items-center gap-2"
            >
              <i
                aria-hidden="true"
                className="size-2.5 rounded-[2px]"
                style={{ background: SEGMENT[segment.kind].background }}
              />
              {segment.counted ? (
                <span>{SEGMENT[segment.kind].name}</span>
              ) : (
                <span data-testid={ids.railNotCounted} className="text-ink-muted">
                  {SEGMENT[segment.kind].name}, not counted
                </span>
              )}
              <span
                className={`tabular-nums ${segment.counted ? 'font-semibold' : 'text-ink-muted'}`}
              >
                {segment.label}
              </span>
            </li>
          ))}
        </ul>
      </section>
      <section className="grid gap-2">
        <div className={OVERLINE}>How it was measured</div>
        <ol className="m-0 grid list-none gap-2 p-0 text-[length:var(--text-sm)]">
          {rowsOf(selected).map((row) => (
            <li
              key={row.key}
              data-testid={ids.railEvent}
              data-kind={row.kind}
              data-counted={row.counted}
              className={`grid grid-cols-[14px_1fr] gap-2 ${row.counted ? '' : 'opacity-65'}`}
            >
              <span
                aria-hidden="true"
                className="mt-1 ml-0.5 box-border size-2.5 rounded-full"
                style={MARK[row.kind]}
              />
              <div>
                {row.body}
                <div className="text-[length:var(--text-xs)] text-ink-muted">{row.atLabel}</div>
              </div>
            </li>
          ))}
        </ol>
      </section>
      <section className="grid gap-1.5">
        <Link
          data-testid={ids.railOpenItem}
          className="btn-ghost justify-between no-underline"
          to={routePath('workItem', { key: projectKey, itemKey: task.key })}
          onClick={onOpenTask}
        >
          Open {task.key} <span aria-hidden="true">↗</span>
        </Link>
        {task.featureKey !== null && (
          <Link
            data-testid={ids.railTimelineLink}
            className="btn-ghost justify-between no-underline"
            to={routePath(
              'featureTimeline',
              { key: projectKey, itemKey: task.featureKey },
              { select: task.key },
            )}
          >
            Feature timeline <span className="font-mono">{task.featureKey}</span>
          </Link>
        )}
      </section>
    </aside>
  );
}
