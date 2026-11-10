import type { DecisionDto, DecisionEventDto } from '@shiplean/core/api/asks';
import { asksTestIds as ids } from './testIds';

const verb: Partial<Record<DecisionEventDto['kind'], string>> = {
  raised: 'raised it, recommending',
  edited: 'edited it to',
  confirmed: 'confirmed',
  changed: 'changed it to',
  withdrawn: 'withdrew it',
};

const dot: Partial<Record<DecisionEventDto['kind'], string>> = {
  changed: 'bg-signal',
  confirmed: 'bg-ok',
  withdrawn: 'bg-ink-muted',
};

// One decision's history, newest first: raised, edited, confirmed, changed (from → to, note) and withdrawn.
export function DecisionHistory({ decision }: { decision: DecisionDto }) {
  return (
    <ul
      data-testid={ids.decisionHistory}
      aria-label={`${decision.label} history`}
      className="col-span-full m-0 grid list-none gap-0 p-0 pl-0.5"
    >
      {[...decision.history].reverse().map((event) => (
        <li
          key={`${event.kind}-${event.at}`}
          className="relative grid gap-px pb-1.5 pl-[18px] text-[length:var(--text-xs)] after:absolute after:top-[14px] after:bottom-0 after:left-[7px] after:w-px after:bg-edge last:after:hidden"
        >
          <span
            aria-hidden="true"
            className={`absolute top-[5px] left-1 size-[7px] rounded-full ${dot[event.kind] ?? 'bg-edge'}`}
          />
          <span>
            <span className="text-ink">
              <b>{event.agentName ?? event.by?.name}</b>
              {event.agentName ? ' (agent)' : ''} {verb[event.kind] ?? event.kind}
              {event.to ? ` “${event.to}”` : ''}
            </span>
            {event.from ? (
              <>
                {' '}
                <span className="text-ink-muted">from</span>{' '}
                <span className="text-ink-muted line-through">{event.from}</span>
              </>
            ) : null}
          </span>
          {event.note ? <span className="text-ink-muted italic">{event.note}</span> : null}
          <span className="text-ink-muted">
            {event.atLabel}
            {event.kind === 'changed' ? ' · posted to Discussion' : ''}
          </span>
        </li>
      ))}
    </ul>
  );
}
