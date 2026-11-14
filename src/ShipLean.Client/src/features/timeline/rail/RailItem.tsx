import type {
  FeatureTimelineViewDto,
  TimelineBarDto,
  TimelineEdgeDto,
  TimelinePersonDto,
  TimelineStoryDto,
} from '@shiplean/core/api/views';
import { Link } from 'react-router-dom';
import { routePath } from '../../../routes';
import { timelineTestIds as ids } from '../testIds';
import { RailKeyButton } from './RailKeyButton';
import { edgesInto, edgesOutOf, projectKeyOf } from './railLookup';

interface Common {
  view: FeatureTimelineViewDto;
  onSelect(key: string | null): void;
}

const person = (p: TimelinePersonDto | null) =>
  p ? (p.agent ? `${p.name} (agent)` : p.name) : 'Unassigned';

const periodName = (state: string, stateName: string) =>
  state === 'blocked' ? 'Blocked' : stateName;

function Header({
  itemKey,
  title,
  projectKey,
  onSelect,
}: {
  itemKey: string;
  title: string;
  projectKey: string;
  onSelect(key: string | null): void;
}) {
  return (
    <div className="flex items-start gap-2">
      <div className="flex-1">
        <Link
          to={routePath('workItem', { key: projectKey, itemKey })}
          className="font-semibold text-signal"
        >
          {itemKey}
        </Link>
        <div className="text-[length:var(--text-sm)]">{title}</div>
      </div>
      <button
        type="button"
        data-testid={ids.railClose}
        aria-label="Close"
        onClick={() => onSelect(null)}
        className="cursor-pointer border-0 bg-transparent text-ink-muted"
      >
        ✕
      </button>
    </div>
  );
}

function EdgeList({
  testId,
  title,
  edges,
  other,
  onSelect,
}: {
  testId: string;
  title: string;
  edges: TimelineEdgeDto[];
  other: 'from' | 'to';
  onSelect(key: string): void;
}) {
  if (edges.length === 0) return null;
  return (
    <section data-testid={testId}>
      <h3 className="m-0 mb-1 text-[length:var(--text-sm)] font-semibold">{title}</h3>
      <ul className="m-0 grid list-none gap-1 p-0 text-[length:var(--text-sm)]">
        {edges.map((e) => (
          <li key={`${e.from}-${e.to}`}>
            <RailKeyButton itemKey={e[other]} onSelect={onSelect} />
            {e.kind === 'blocked' && ' (blocked it)'}
            {e.waitLabel && <span className="text-ink-muted"> · {e.waitLabel}</span>}
          </li>
        ))}
      </ul>
    </section>
  );
}

export function RailTask({ view, bar, onSelect }: Common & { bar: TimelineBarDto }) {
  return (
    <div data-testid={ids.railSelected} data-key={bar.key} className="grid gap-3 p-4">
      <Header
        itemKey={bar.key}
        title={bar.title}
        projectKey={projectKeyOf(view)}
        onSelect={onSelect}
      />
      <div className="text-[length:var(--text-sm)]">
        <div>{person(bar.assignee)}</div>
        {bar.inferred ? (
          <div>Closed, no start recorded · {bar.endLabel}</div>
        ) : (
          <div>
            {bar.startLabel} to {bar.endLabel}
            {bar.durationLabel && ` · ${bar.durationLabel}`}
          </div>
        )}
        {bar.onCriticalPath && <div className="font-medium">On the critical path</div>}
      </div>
      <section>
        <h3 className="m-0 mb-1 text-[length:var(--text-sm)] font-semibold">History</h3>
        <ul className="m-0 grid list-none gap-1 p-0 text-[length:var(--text-sm)]">
          {bar.history.map((p) => (
            <li key={`${p.state}-${p.from}`} data-testid={ids.railHistoryRow} data-state={p.state}>
              {periodName(p.state, p.stateName)} · {p.fromLabel}
              {p.durationLabel && ` · ${p.durationLabel}`}
            </li>
          ))}
        </ul>
      </section>
      {bar.reviews.length > 0 && (
        <section>
          <h3 className="m-0 mb-1 text-[length:var(--text-sm)] font-semibold">Reviews</h3>
          <ul className="m-0 grid list-none gap-1 p-0 text-[length:var(--text-sm)]">
            {bar.reviews.map((r) => (
              <li key={r.at} data-testid={ids.railReviewRow} data-verdict={r.verdict}>
                <span
                  style={{
                    color: r.verdict === 'approved' ? 'var(--color-ok)' : 'var(--color-warn)',
                  }}
                >
                  {r.verdict === 'approved' ? 'Approved' : 'Changes requested'}
                </span>{' '}
                · {person(r.by)} · {r.atLabel}
                {r.note && <div className="text-ink-muted">{r.note}</div>}
              </li>
            ))}
          </ul>
        </section>
      )}
      <EdgeList
        testId={ids.railWaitedOn}
        title="Waited on"
        edges={edgesInto(view, bar.key)}
        other="from"
        onSelect={onSelect}
      />
      <EdgeList
        testId={ids.railHeldUp}
        title="Held up"
        edges={edgesOutOf(view, bar.key)}
        other="to"
        onSelect={onSelect}
      />
    </div>
  );
}

export function RailStory({ view, story, onSelect }: Common & { story: TimelineStoryDto }) {
  return (
    <div data-testid={ids.railSelected} data-key={story.key} className="grid gap-3 p-4">
      <Header
        itemKey={story.key}
        title={story.title}
        projectKey={projectKeyOf(view)}
        onSelect={onSelect}
      />
      <div className="text-[length:var(--text-sm)]">
        {story.startLabel ? (
          <div>
            {story.startLabel} to {story.endLabel}
            {story.durationLabel && ` · ${story.durationLabel}`}
          </div>
        ) : (
          <div>Not started</div>
        )}
      </div>
      <section>
        <h3 className="m-0 mb-1 text-[length:var(--text-sm)] font-semibold">Tasks</h3>
        <ul className="m-0 grid list-none gap-1 p-0 text-[length:var(--text-sm)]">
          {story.tasks.map((key) => (
            <li key={key}>
              <RailKeyButton itemKey={key} onSelect={onSelect} />{' '}
              {view.bars.find((b) => b.key === key)?.title}
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}
