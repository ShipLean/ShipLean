import { Link } from 'react-router-dom';
import type { SprintDemoDto, SprintDemoStatus } from '@shiplean/core/api/demos';
import { Chip, type ChipTone } from '../../components/Chip';
import { showToast } from '../../components/toastStore';
import { useCreateSprintDemo } from './demosApi';
import type { DemoProps } from './DemoProps';
import { demosTestIds as ids } from './testIds';

const BAU_NOTE =
  'It runs as a BAU item (Business As Usual: background work that is never on a sprint), so it never counts in your velocity.';

interface BandLook {
  chip: string;
  tone: ChipTone;
  edge: string;
}

const LOOKS: Record<SprintDemoStatus, BandLook> = {
  closable: { chip: 'Not closed', tone: 'accent', edge: 'border-l-notice' },
  none: { chip: 'No demo yet', tone: 'accent', edge: 'border-l-notice' },
  requested: { chip: 'Requested', tone: 'accent', edge: 'border-l-notice' },
  working: { chip: 'Working', tone: 'accent', edge: 'border-l-notice' },
  ready: { chip: 'Ready', tone: 'ok', edge: 'border-l-good' },
  failed: { chip: 'Failed', tone: 'danger', edge: 'border-l-alert' },
  blocked: { chip: 'Blocked', tone: 'warn', edge: 'border-l-caution' },
};

function headline(demo: SprintDemoDto): string {
  if (demo.statusMessage !== null) return demo.statusMessage;
  switch (demo.status) {
    case 'closable':
      return 'Close the sprint to make its demo.';
    case 'none':
      return `Make a demo of ${demo.sprintName}.`;
    case 'requested':
      return 'The demo is requested.';
    case 'working':
      return 'An agent is making the demo.';
    case 'ready':
      return `The demo for ${demo.sprintName} is ready.`;
    case 'failed':
      return 'The demo did not arrive.';
    case 'blocked':
      return 'The demo did not start.';
  }
}

// The line under the headline: why Create is off when it is, else what the status needs explaining.
function detail(demo: SprintDemoDto): string | null {
  if (!demo.actions.canCreate && demo.actions.createBlockedMessage !== null) {
    return demo.actions.createBlockedMessage;
  }
  if (demo.status === 'closable') {
    return `A demo is a short video of what the sprint finished. It's made by an agent working a BAU item (Business As Usual: background work that is never on a sprint).`;
  }
  if (demo.status === 'none') {
    return `An agent starts a seeded copy of the project, records a walk-through of what this sprint moved, and uploads the video here. ${BAU_NOTE}`;
  }
  return null;
}

function runLabels(demo: SprintDemoDto): string[] {
  const { run, video } = demo;
  if (run === null) return [];
  if (demo.status === 'ready') return video === null ? [] : [video.deliveredLabel];
  const labels = [run.requestedLabel];
  if (demo.status === 'working' || demo.status === 'requested') {
    if (run.sessionState !== null) labels.push(`Agent session: ${run.sessionState}`);
    if (run.uploadExpiresLabel !== null) labels.push(run.uploadExpiresLabel);
  }
  return labels;
}

function createLabel(status: SprintDemoStatus): string {
  if (status === 'failed') return 'Create demo again';
  if (status === 'ready') return 'Remake demo';
  return 'Create demo';
}

const buttonClass =
  'h-7 rounded-control bg-signal px-3 text-[length:var(--text-sm)] font-semibold whitespace-nowrap text-signal-ink hover:brightness-[1.08] disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:brightness-100 touch:order-3 touch:h-[var(--touch-target)] touch:w-full';

/** The tab's status band: the status chip, what it means, the run's labels with the BAU item's link, and Create demo. */
export function DemoStatusBand({ demo }: DemoProps) {
  const create = useCreateSprintDemo(demo.sprintId);
  const look = LOOKS[demo.status];
  const note = detail(demo);
  const labels = runLabels(demo);
  const { actions, run } = demo;
  const blocked = !actions.canCreate && actions.createBlockedMessage !== null;
  return (
    <div
      data-testid={ids.demoStatusBand}
      data-status={demo.status}
      className={`flex flex-wrap items-center gap-3 rounded-panel border border-l-[3px] border-edge bg-panel px-3 py-2 touch:grid touch:grid-cols-[minmax(0,1fr)] touch:justify-items-start touch:gap-2 ${look.edge}`}
    >
      <Chip tone={look.tone}>{look.chip}</Chip>
      <div className="grid min-w-60 flex-1 gap-0.5 text-[length:var(--text-sm)] [overflow-wrap:anywhere] touch:contents">
        <b
          data-testid={ids.demoStatusMessage}
          className="text-[length:var(--text-base)] touch:order-1"
        >
          {headline(demo)}
        </b>
        {note !== null && (
          <span
            data-testid={blocked ? ids.demoCreateReason : undefined}
            className={`text-ink-muted ${blocked ? 'touch:order-4' : 'touch:order-2'}`}
          >
            {note}
          </span>
        )}
        {(labels.length > 0 || run !== null) && (
          <span className="flex flex-wrap items-center gap-x-2 text-ink-muted touch:order-2 touch:w-full">
            {labels.join(' · ')}
            {run !== null && (
              <Link
                data-testid={ids.demoItemLink}
                to={run.itemUrl}
                className="text-signal touch:inline-flex touch:min-h-[var(--touch-target)] touch:basis-full touch:items-center"
              >
                {run.itemKey}
              </Link>
            )}
          </span>
        )}
      </div>
      {demo.status === 'working' && (
        <span
          aria-hidden="true"
          className="size-2 shrink-0 animate-pulse rounded-full bg-notice motion-reduce:animate-none touch:hidden"
        />
      )}
      {actions.canCreate ? (
        <button
          type="button"
          data-testid={ids.demoCreate}
          disabled={create.isPending}
          className={buttonClass}
          onClick={() =>
            create.mutate(undefined, {
              onError: (failure) => showToast(failure.message, { tone: 'danger' }),
            })
          }
        >
          {createLabel(demo.status)}
        </button>
      ) : (
        demo.status !== 'working' &&
        demo.status !== 'closable' &&
        actions.createBlockedMessage !== null && (
          <button
            type="button"
            data-testid={ids.demoCreateBlocked}
            disabled
            className={buttonClass}
          >
            Create demo
          </button>
        )
      )}
    </div>
  );
}
