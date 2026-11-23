import type {
  FeatureTimelineViewDto,
  TimelineBarDto,
  TimelineEdgeDto,
  TimelinePersonDto,
  TimelineStoryDto,
} from '@shiplean/core/api/views';
import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { routePath } from '../../../routes';
import { PersonAvatar } from '../parts/PersonAvatar';
import { TypeBadge } from '../parts/TypeBadge';
import { timelineTestIds as ids } from '../testIds';
import { RailKeyButton } from './RailKeyButton';
import { edgesInto, edgesOutOf, projectKeyOf } from './railLookup';

interface Common {
  view: FeatureTimelineViewDto;
  onSelect(key: string | null): void;
}

const periodName = (state: string, stateName: string) =>
  state === 'blocked' ? 'Blocked' : stateName;

const periodColour: Record<string, string> = {
  new: 'var(--color-muted)',
  active: 'var(--chart-completed)',
  blocked: 'var(--color-danger)',
  resolved: 'var(--color-ok)',
  closed: 'var(--color-muted)',
  removed: 'var(--color-muted)',
};

const verdictColour = (verdict: string) =>
  verdict === 'approved' ? 'var(--color-ok)' : 'var(--color-warn)';

function Dot({ colour }: { colour: string }) {
  return (
    <i
      aria-hidden="true"
      className="inline-block size-2 flex-none rounded-full"
      style={{ background: colour }}
    />
  );
}

function Person({ person }: { person: TimelinePersonDto | null }) {
  return (
    <span className="inline-flex items-center gap-1.5">
      <PersonAvatar person={person} size="sm" />
      {person ? person.name : 'Unassigned'}
      {person?.agent && (
        <span
          data-testid={ids.railAgentChip}
          className="rounded-sm border border-edge px-1 text-[length:var(--text-xs)] text-ink-muted"
        >
          Agent
        </span>
      )}
    </span>
  );
}

function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <>
      <dt className="text-ink-muted">{label}</dt>
      <dd className="m-0 flex flex-wrap items-center gap-1.5">{children}</dd>
    </>
  );
}

function Facts({ children }: { children: ReactNode }) {
  return (
    <dl
      data-testid={ids.railFacts}
      className="m-0 grid grid-cols-[5.5rem_1fr] gap-x-3 gap-y-1.5 text-[length:var(--text-sm)]"
    >
      {children}
    </dl>
  );
}

function Header({
  type,
  itemKey,
  title,
  onSelect,
}: {
  type: 'task' | 'story' | 'bug';
  itemKey: string;
  title: string;
  onSelect(key: string | null): void;
}) {
  return (
    <div className="flex items-start gap-2">
      <TypeBadge type={type} />
      <div className="min-w-0 flex-1">
        <div className="font-mono text-[length:var(--text-xs)] text-ink-muted">{itemKey}</div>
        <h2 className="m-0 text-[length:var(--text-sm)] font-semibold">{title}</h2>
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

function Footer({
  view,
  itemKey,
  onSelect,
}: {
  view: FeatureTimelineViewDto;
  itemKey: string;
  onSelect(key: string | null): void;
}) {
  return (
    <>
      <div>
        <Link
          data-testid={ids.railOpen}
          to={routePath('workItem', { key: projectKeyOf(view), itemKey })}
          className="inline-block rounded-md border border-edge px-2.5 py-1 text-[length:var(--text-sm)] font-medium text-ink"
        >
          Open {itemKey}
        </Link>
      </div>
      <div>
        <button
          type="button"
          data-testid={ids.railBack}
          onClick={() => onSelect(null)}
          className="cursor-pointer border-0 bg-transparent p-0 text-[length:var(--text-sm)] text-signal hover:underline"
        >
          ← Reading the chart
        </button>
      </div>
    </>
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

export function RailTask({
  view,
  bar,
  onSelect,
  runs,
}: Common & { bar: TimelineBarDto; runs?: ReactNode }) {
  const parent = view.stories.find((s) => s.key === bar.parent);
  const finished = bar.endLabel !== null;
  const firstReview = bar.reviews[0];
  return (
    <div data-testid={ids.railSelected} data-key={bar.key} className="grid gap-3 p-4">
      <Header type="task" itemKey={bar.key} title={bar.title} onSelect={onSelect} />
      <Facts>
        <Fact label="Assignee">
          <Person person={bar.assignee} />
        </Fact>
        {parent && (
          <Fact label={parent.type === 'bug' ? 'Bug' : 'Story'}>
            <RailKeyButton itemKey={parent.key} onSelect={onSelect} /> {parent.title}
          </Fact>
        )}
        <Fact label="Active">
          {bar.startLabel ?? <span className="text-ink-muted">Not recorded</span>}
        </Fact>
        <Fact label={bar.inferred ? 'Closed' : 'Resolved'}>
          {bar.endLabel ?? <span className="text-ink-muted">Not yet</span>}
        </Fact>
        <Fact label="Took">
          <b>{bar.durationLabel ?? '—'}</b>
          {bar.onCriticalPath && finished && (
            <span
              data-testid={ids.railCriticalPill}
              className="rounded-full border border-edge px-2 text-[length:var(--text-xs)] font-medium"
            >
              Critical path
            </span>
          )}
        </Fact>
        {firstReview && <Fact label="Reviewed">{firstReview.atLabel}</Fact>}
        {bar.lastCommitLabel !== null && <Fact label="Last commit">{bar.lastCommitLabel}</Fact>}
      </Facts>
      {bar.inferred && (
        <p
          data-testid={ids.railNoStart}
          className="m-0 text-[length:var(--text-xs)] text-ink-muted"
        >
          Closed in one bulk edit, so no start was recorded; it shows as a marker.
        </p>
      )}
      <section>
        <h3 className="m-0 mb-1 text-[length:var(--text-sm)] font-semibold">History</h3>
        <ul className="m-0 grid list-none gap-1 p-0 text-[length:var(--text-sm)]">
          {bar.history.map((p) => (
            <li
              key={`${p.state}-${p.from}`}
              data-testid={ids.railHistoryRow}
              data-state={p.state}
              className="flex items-center gap-2"
            >
              <Dot colour={periodColour[p.state] ?? 'var(--color-muted)'} />
              <span className="flex-1">
                {periodName(p.state, p.stateName)} · {p.fromLabel}
              </span>
              {p.durationLabel && (
                <span data-duration className="text-right text-ink-muted">
                  {p.durationLabel}
                </span>
              )}
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
                <div className="flex items-center gap-2">
                  <Dot colour={verdictColour(r.verdict)} />
                  <span className="flex-1">
                    {r.verdict === 'approved' ? 'Approved' : 'Changes requested'} · {r.atLabel}
                  </span>
                  <span className="text-right text-ink-muted">{r.by.name}</span>
                </div>
                {r.note && <div className="pl-4 text-ink-muted">{r.note}</div>}
              </li>
            ))}
          </ul>
        </section>
      )}
      {runs}
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
      <Footer view={view} itemKey={bar.key} onSelect={onSelect} />
    </div>
  );
}

export function RailStory({ view, story, onSelect }: Common & { story: TimelineStoryDto }) {
  return (
    <div data-testid={ids.railSelected} data-key={story.key} className="grid gap-3 p-4">
      <Header type={story.type} itemKey={story.key} title={story.title} onSelect={onSelect} />
      <Facts>
        <Fact label="Owner">
          <Person person={story.assignee} />
        </Fact>
        <Fact label="Active">
          {story.startLabel ?? <span className="text-ink-muted">Not started</span>}
        </Fact>
        <Fact label="Resolved">
          {story.endLabel ?? <span className="text-ink-muted">Not yet</span>}
        </Fact>
        <Fact label="Took">
          <b>{story.durationLabel ?? '—'}</b>
        </Fact>
      </Facts>
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
      <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
        A story&apos;s bracket runs from its first task going Active to its last task going
        Resolved.
      </p>
      <Footer view={view} itemKey={story.key} onSelect={onSelect} />
    </div>
  );
}
