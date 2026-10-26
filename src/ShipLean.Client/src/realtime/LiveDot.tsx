import { realtimeTestIds } from '../features/realtime/testIds';
import type { ConnectionState } from './connectionState';

const labels: Record<ConnectionState, string> = {
  connecting: 'Connecting',
  live: 'Live',
  reconnecting: 'Reconnecting',
  offline: 'Offline',
};

/** The top bar's Live dot: green while connected, warn (and saying why) otherwise. */
export function LiveDot({ state }: { state: ConnectionState }) {
  const colour = state === 'live' ? '--color-ok' : '--color-warn';
  return (
    <span
      data-testid={realtimeTestIds.liveDot}
      data-state={state}
      className="inline-flex items-center gap-1.5 text-[length:var(--text-xs)] font-semibold"
      style={{ color: `var(${colour})` }}
    >
      <span
        aria-hidden="true"
        className="size-[7px] rounded-full"
        style={{
          background: `var(${colour})`,
          boxShadow: `0 0 0 3px color-mix(in srgb, var(${colour}) 22%, transparent)`,
        }}
      />
      {labels[state]}
    </span>
  );
}
