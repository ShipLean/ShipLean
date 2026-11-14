import { timelineTestIds as ids } from './testIds';
import type { TimelineOptions } from './TimelineOptions';
import type { TimelineToolbarProps } from './TimelineToolbarProps';

interface SegmentProps<T extends string> {
  testId: string;
  label: string;
  value: T;
  choices: readonly (readonly [T, string])[];
  onPick(value: T): void;
}

function Segment<T extends string>({ testId, label, value, choices, onPick }: SegmentProps<T>) {
  return (
    <div
      data-testid={testId}
      role="group"
      aria-label={label}
      className="inline-flex overflow-hidden rounded-md border border-edge"
    >
      {choices.map(([choice, text]) => (
        <button
          key={choice}
          type="button"
          data-value={choice}
          aria-pressed={choice === value}
          onClick={() => onPick(choice)}
          className={`px-2.5 py-1 text-[length:var(--text-sm)] ${
            choice === value ? 'bg-signal-soft font-semibold text-ink' : 'bg-panel text-ink-muted'
          }`}
        >
          {text}
        </button>
      ))}
    </div>
  );
}

function Swatch({ colour, label }: { colour: string; label: string }) {
  return (
    <span className="inline-flex items-center gap-1">
      <i
        aria-hidden="true"
        className="inline-block h-2 w-4 rounded-sm"
        style={{ background: colour }}
      />
      {label}
    </span>
  );
}

const VIEW_CHOICES = [
  ['chart', 'Chart'],
  ['json', 'JSON for agents'],
] as const;

/** Grouping, zoom, the two overlays, the legend and the Chart/JSON switch; every change is one onChange call. */
export function TimelineToolbar({ options, finished, onChange }: TimelineToolbarProps) {
  const set = (patch: Partial<TimelineOptions>) => onChange({ ...options, ...patch });
  const switcher = (
    <Segment
      testId={ids.toolbarView}
      label="View"
      value={options.view}
      choices={VIEW_CHOICES}
      onPick={(view) => set({ view })}
    />
  );
  return (
    <div
      data-testid={ids.toolbar}
      data-group={options.group}
      data-zoom={options.zoom}
      data-deps={options.deps}
      data-cp={options.cp}
      data-view={options.view}
      className="flex flex-wrap items-center gap-3 border-b border-edge bg-panel px-4 py-2 text-[length:var(--text-sm)]"
    >
      {options.view === 'json' ? (
        <>
          <b>What agents get</b>
          <span className="text-ink-muted">
            The same view model as the chart, from <code>feature_timeline</code>. Times are UTC.
          </span>
          <span className="flex-1" />
          {switcher}
        </>
      ) : (
        <>
          <span className="text-ink-muted">Group</span>
          <Segment
            testId={ids.toolbarGroup}
            label="Group"
            value={options.group}
            choices={[
              ['stories', 'Stories'],
              ['people', 'People'],
              ['tasks', 'Tasks'],
            ]}
            onPick={(group) => set({ group })}
          />
          <span className="text-ink-muted">Zoom</span>
          <Segment
            testId={ids.toolbarZoom}
            label="Zoom"
            value={options.zoom}
            choices={[
              ['fit', 'Fit'],
              ['days', 'Days'],
            ]}
            onPick={(zoom) => set({ zoom })}
          />
          <label className="inline-flex items-center gap-1.5">
            <input
              data-testid={ids.toolbarDeps}
              type="checkbox"
              checked={options.deps}
              onChange={(event) => set({ deps: event.target.checked })}
            />
            Dependencies
          </label>
          <label
            className={`inline-flex items-center gap-1.5 ${finished ? '' : 'text-ink-muted opacity-60'}`}
            title={
              finished
                ? 'The chain of tasks that set the finish date'
                : 'Shows once the Feature is Done'
            }
          >
            <input
              data-testid={ids.toolbarCp}
              type="checkbox"
              checked={options.cp}
              disabled={!finished}
              onChange={(event) => set({ cp: event.target.checked })}
            />
            Critical path
          </label>
          <span className="flex-1" />
          <span
            data-testid={ids.toolbarLegend}
            className="inline-flex flex-wrap items-center gap-3 text-[length:var(--text-xs)] text-ink-muted"
          >
            <Swatch colour="var(--chart-completed)" label="Task" />
            <Swatch colour="var(--color-ok)" label="Approved" />
            <Swatch colour="var(--color-warn)" label="Changes asked" />
            {finished ? <Swatch colour="var(--color-text)" label="Critical path" /> : null}
          </span>
          {switcher}
        </>
      )}
    </div>
  );
}
