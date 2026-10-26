import { componentTestIds } from './testIds';

/** The real-time connection's state: a warn banner while reconnecting, an info one once it is back. */
export function ReconnectBanner({
  state,
  changeCount,
  onRetry,
}: {
  state: 'reconnecting' | 'reconnected';
  /** Reconnected only: how many changes arrived while the connection was down. */
  changeCount?: number;
  onRetry?: () => void;
}) {
  const reconnecting = state === 'reconnecting';
  return (
    <div
      data-testid={componentTestIds.reconnectBanner}
      data-state={state}
      role="status"
      className="flex items-center gap-2 rounded-control border border-edge bg-panel px-3 py-2 text-[length:var(--text-sm)] text-ink"
      style={{ borderLeft: `3px solid var(${reconnecting ? '--color-warn' : '--color-ok'})` }}
    >
      <b>{reconnecting ? 'Connection lost. Reconnecting…' : 'Reconnected'}</b>
      {!reconnecting && changeCount !== undefined ? (
        <span>{changeCount === 1 ? '1 change arrived.' : `${changeCount} changes arrived.`}</span>
      ) : null}
      {reconnecting && onRetry ? (
        <button
          type="button"
          data-testid={componentTestIds.reconnectBannerRetry}
          className="btn-ghost ml-auto"
          onClick={onRetry}
        >
          Retry now
        </button>
      ) : null}
    </div>
  );
}
