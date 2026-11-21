import { Link, useLocation } from 'react-router-dom';
import type { SprintDemoDto, SprintDemoStatus } from '@shiplean/core/api/demos';
import { Chip, type ChipTone } from '../../components/Chip';
import { showToast } from '../../components/toastStore';
import { TypeIcon } from '../../components/TypeIcon';
import { useTouchShell } from '../../touch/useTouchShell';
import { useCreateSprintDemo } from './demosApi';
import { DemoCreateWarning } from './DemoCreateWarning';
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

// On a phone the requested label names who asked (SHIP-1810 P2); the desktop band keeps the plain label.
function runLabels(demo: SprintDemoDto, touch: boolean): string[] {
  const { run, video } = demo;
  if (run === null) return [];
  if (demo.status === 'ready') return video === null ? [] : [video.deliveredLabel];
  const labels = [touch ? (run.requestedByLabel ?? run.requestedLabel) : run.requestedLabel];
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
  'h-7 rounded-control bg-signal px-3 text-[length:var(--text-sm)] font-semibold whitespace-nowrap text-signal-ink hover:brightness-[1.08] disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:brightness-100 touch:order-5 touch:col-span-2 touch:h-[var(--touch-target)] touch:w-full';

/**
 * The tab's status band: the status chip, what it means, the run's labels with the BAU item's link, and Create demo.
 * On a phone it stacks (SHIP-1810 P2): the chip with the Working pulse at its right, the message, the line under it, what the
 * agent said when its session ended (P5), the BAU item as its own row with its type icon and title, then the action across
 * the width; a disabled action keeps its place, the line above says why.
 */
export function DemoStatusBand({ demo }: DemoProps) {
  const { touch } = useTouchShell();
  const { pathname } = useLocation();
  const create = useCreateSprintDemo(demo.sprintId);
  const look = LOOKS[demo.status];
  const note = detail(demo);
  const labels = runLabels(demo, touch);
  const { actions, run } = demo;
  // On a phone the agent's words leave the message for a block of their own (P5); the desktop keeps the one sentence.
  const said = touch ? demo.agentSaid : null;
  const blocked = !actions.canCreate && actions.createBlockedMessage !== null;
  return (
    <div
      data-testid={ids.demoStatusBand}
      data-status={demo.status}
      className={`flex flex-wrap items-center gap-3 rounded-panel border border-l-[3px] border-edge bg-panel px-3 py-2 touch:grid touch:grid-cols-[minmax(0,1fr)_auto] touch:justify-items-start touch:gap-2 ${look.edge}`}
    >
      <Chip tone={look.tone}>{look.chip}</Chip>
      <div className="grid min-w-60 flex-1 gap-0.5 text-[length:var(--text-sm)] [overflow-wrap:anywhere] touch:contents">
        <b
          data-testid={ids.demoStatusMessage}
          className="text-[length:var(--text-base)] touch:order-1 touch:col-span-2"
        >
          {said !== null ? said.message : headline(demo)}
        </b>
        {note !== null && (
          <span
            data-testid={blocked ? ids.demoCreateReason : undefined}
            className="text-ink-muted touch:order-2 touch:col-span-2"
          >
            {note}
          </span>
        )}
        {(labels.length > 0 || run !== null) && (
          <span className="flex flex-wrap items-center gap-x-2 text-ink-muted touch:contents">
            {labels.length > 0 && (
              <span className="touch:order-2 touch:col-span-2">{labels.join(' · ')}</span>
            )}
            {run !== null && (
              <Link
                data-testid={ids.demoItemLink}
                to={run.itemUrl}
                state={{ backTo: pathname, backLabel: 'Demo' }}
                className="text-signal touch:order-4 touch:col-span-2 touch:flex touch:min-h-[var(--touch-target)] touch:w-full touch:items-center touch:justify-between touch:gap-2 touch:font-mono touch:text-ink"
              >
                {touch ? (
                  <span className="flex min-w-0 items-center gap-2">
                    <TypeIcon type={run.itemType} />
                    <span className="flex-none">{run.itemKey}</span>
                    <span className="truncate text-ink-muted">{run.itemTitle}</span>
                  </span>
                ) : (
                  run.itemKey
                )}
                {touch && (
                  <span aria-hidden="true" className="text-ink-muted">
                    ›
                  </span>
                )}
              </Link>
            )}
          </span>
        )}
        {said !== null && (
          <blockquote
            data-testid={ids.demoAgentSaid}
            className="m-0 rounded-r-control border-l-[3px] border-edge bg-sunken px-2.5 py-1.5 text-ink [overflow-wrap:anywhere] touch:order-3 touch:col-span-2 touch:w-full"
          >
            <span className="block font-semibold text-ink-muted">It said</span>
            {said.text}
          </blockquote>
        )}
      </div>
      {demo.status === 'working' && (
        <span
          aria-hidden="true"
          className="size-2 shrink-0 animate-pulse rounded-full bg-notice motion-reduce:animate-none touch:col-start-2 touch:row-start-1 touch:self-center"
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
        // A closable sprint's Create stays in place, off, on a phone (P2); the desktop band leaves it out.
        (demo.status === 'closable' ? touch : actions.createBlockedMessage !== null) && (
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
      <DemoCreateWarning actions={actions} />
    </div>
  );
}
